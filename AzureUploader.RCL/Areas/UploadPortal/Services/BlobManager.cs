using AzureUploader.RCL.Areas.AzureUploader.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AzureUploader.RCL.Areas.AzureUploader.Services
{
    public abstract class BlobManager
    {
        protected readonly StorageCredentials StorageCredentials;

        private const string mdUserName = "userName";
        private const string mdSourceFile = "sourceFile";

        public BlobManager(StorageCredentials storageCredentials)
        {
            StorageCredentials = storageCredentials;
        }

        /// <summary>
        /// where do user uploads go? This is the initial destination of all uploads
        /// </summary>
        protected abstract string UploadContainerName { get; }

        /// <summary>
        /// where do uploads go when submitted? This is intended to vary by user, reflecting their tenant association
        /// </summary>
        protected abstract Task<CloudBlobContainer> GetSubmittedContainerAsync(string userName);

        protected abstract Task<int> LogSubmitStartedAsync(SubmittedBlob submittedBlob);

        protected abstract Task LogSubmitDoneAsync(int id, bool successful, string message = null);

        protected abstract Task<IEnumerable<SubmittedBlob>> QuerySubmittedBlobsAsync(string userName, int pageSize = 30, int page = 0);

        private string GetBlobName(IPrincipal user, IFormFile file, string path = null)
        {
            string[] parts = new string[]
            {
                GetUserFolderName(user),
                path,
                file.FileName
            };

            return Path.Combine(parts.Where(p => !string.IsNullOrEmpty(p)).ToArray());
        }

        protected virtual Task OnBlobUploaded(IPrincipal user, CloudBlockBlob blob) => Task.CompletedTask;

        protected string GetUserFolderName(IPrincipal user) => (user.Identity.IsAuthenticated) ? user.Identity.Name : "anonUser";

        protected async Task<CloudBlobContainer> GetContainerInternalAsync(string containerName)
        {
            var account = new CloudStorageAccount(StorageCredentials, true);
            var client = new CloudBlobClient(account.BlobStorageUri, StorageCredentials); ;
            var container = client.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public async Task UploadAsync(HttpRequest request, IPrincipal user, string path = null)
        {
            var container = await GetContainerInternalAsync(UploadContainerName);

            foreach (var file in request.Form.Files)
            {
                using (var stream = file.OpenReadStream())
                {
                    string blobName = GetBlobName(user, file, path);
                    var blob = container.GetBlockBlobReference(blobName);
                    blob.Properties.ContentType = file.ContentType;                    

                    await blob.UploadFromStreamAsync(stream);

                    blob.Metadata.Add(mdUserName, GetUserFolderName(user));
                    blob.Metadata.Add(mdSourceFile, file.FileName);
                    blob.Metadata.Add("timestamp", DateTime.UtcNow.ToString());
                    await blob.SetMetadataAsync();

                    await OnBlobUploaded(user, blob);
                }
            }
        }

        public async Task<IEnumerable<CloudBlockBlob>> GetMyBlobsAsync(IPrincipal user)
        {
            var container = await GetContainerInternalAsync(UploadContainerName);

            List<CloudBlockBlob> results = new List<CloudBlockBlob>();
            var token = default(BlobContinuationToken);
            var dir = container.GetDirectoryReference(GetUserFolderName(user));
            do
            {
                var segment = await dir.ListBlobsSegmentedAsync(true, BlobListingDetails.All, null, token, null, null);
                results.AddRange(segment.Results.OfType<CloudBlockBlob>());
                token = segment.ContinuationToken;
            } while (token != null);

            return results;
        }

        public async Task SubmitBlobAsync(CloudBlockBlob blob)
        {
            if (!(await blob.ExistsAsync())) throw new Exception($"Source blob {blob.Uri.AbsoluteUri} not found.");

            // where is it going? this depends on who uploaded it
            string userName = GetBlobUserName(blob);
            var container = await GetSubmittedContainerAsync(userName);

            // make sure we're not overwriting the source
            string destName = blob.Name;
            var destBlob = container.GetBlockBlobReference(destName);
            destBlob.Properties.ContentType = blob.Properties.ContentType;
            if (blob.Uri.Equals(destBlob.Uri)) throw new InvalidOperationException("Source and destination blobs cannot be the same.");

            // if the target already exists, it means we have a new, better version of it -- and we want to log that this is an overwrite
            bool exists = await destBlob.ExistsAsync();
            await destBlob.DeleteIfExistsAsync();

            int logId = await LogSubmitStartedAsync(new SubmittedBlob()
            {
                Timestamp = DateTime.UtcNow,
                UserName = userName,
                Path = TrimUserName(userName, blob.Name),
                Length = blob.Properties.Length,
                IsOverwrite = exists
            });

            try
            {
                using (var source = blob.OpenRead())
                {
                    await destBlob.UploadFromStreamAsync(source);
                }

                await blob.DeleteAsync();

                await LogSubmitDoneAsync(logId, true);
            }
            catch (Exception exc)
            {
                await LogSubmitDoneAsync(logId, false, exc.Message);
            }
        }

        public async Task SubmitBlobAsync(string blobUri)
        {          
            var sourceUri = new Uri(blobUri);
            var sourceBlob = new CloudBlockBlob(sourceUri, StorageCredentials);
            await SubmitBlobAsync(sourceBlob);
        }

        private string GetBlobUserName(CloudBlockBlob blob)
        {
            try
            {
                return blob.Metadata[mdUserName];
            }
            catch
            {
                // this is because early versions weren't writing metadata correctly, and I needed a fallback by inspecting the blob name.
                // the user name is always the first part of the name
                var nameParts = blob.Name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return nameParts.First();
            }
        }

        public async Task<IEnumerable<SubmittedBlob>> GetMySubmittedBlobsAsync(IPrincipal user, int pageSize = 30, int page = 0)
        {
            string userName = GetUserFolderName(user);
            return await QuerySubmittedBlobsAsync(userName, pageSize, page);
        }

        public string TrimUserName(string userName, string path)
        {
            var nameParts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return (nameParts.First().Equals(userName)) ?
                string.Join('/', nameParts.Skip(1)) :
                string.Join('/', nameParts);
        }

        public string GetDownloadUrl(string blobUri)
        {
            var blob = new CloudBlockBlob(new Uri(blobUri), StorageCredentials);
            var token = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTime.UtcNow.Add(TimeSpan.FromHours(1))
            });

            return blob.Uri.AbsoluteUri + token;
        }
    }
}
