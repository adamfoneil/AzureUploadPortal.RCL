using AzureUploader.RCL.Areas.AzureUploader.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
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
        /// where do uploads go when "processed"?
        /// </summary>
        protected abstract Task<CloudBlobContainer> GetSubmittedContainerAsync(string userName);

        protected abstract Task<int> LogSubmitStartedAsync(SubmittedBlob submittedBlob);

        protected abstract Task LogSubmitDoneAsync(int id, bool successful, string message = null);

        protected virtual string GetBlobName(IPrincipal user, IFormFile file) => file.FileName;

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

        public async Task UploadAsync(HttpRequest request, IPrincipal user)
        {
            var container = await GetContainerInternalAsync(UploadContainerName);

            foreach (var file in request.Form.Files)
            {
                using (var stream = file.OpenReadStream())
                {
                    string blobName = GetBlobName(user, file);
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

        public async Task SubmitBlobAsync(string blobUri)
        {
            // verify we have the source blob
            var sourceUri = new Uri(blobUri);
            var sourceBlob = new CloudBlockBlob(sourceUri, StorageCredentials);
            if (!(await sourceBlob.ExistsAsync())) throw new Exception($"Source blob {blobUri} not found.");

            // where is it going? this depends on who uploaded it
            string userName = GetBlobUserName(sourceBlob);
            var container = await GetSubmittedContainerAsync(userName);

            // make sure we're not overwriting the source
            string destName = sourceBlob.Name;            
            var destBlob = container.GetBlockBlobReference(destName);
            destBlob.Properties.ContentType = sourceBlob.Properties.ContentType;
            if (sourceBlob.Uri.Equals(destBlob.Uri)) throw new InvalidOperationException("Source and destination blobs cannot be the same.");

            // if the target already exists, it means we have a new, better version of it -- and we want to log that this is an overwrite
            bool exists = await destBlob.ExistsAsync();
            await destBlob.DeleteIfExistsAsync();
            
            int logId = await LogSubmitStartedAsync(new SubmittedBlob()
            {
                Timestamp = DateTime.UtcNow,
                UserName = userName,
                Path = sourceBlob.Name,
                Length = sourceBlob.Properties.Length,
                IsOverwrite = exists
            });

            try
            {
                using (var source = sourceBlob.OpenRead())
                {
                    await destBlob.UploadFromStreamAsync(source);
                }

                await sourceBlob.DeleteAsync();

                await LogSubmitDoneAsync(logId, true);
            }
            catch (Exception exc)
            {
                await LogSubmitDoneAsync(logId, false, exc.Message);
            }
        }

        private string GetBlobUserName(CloudBlockBlob blob)
        {
            try
            {
                return blob.Metadata[mdUserName];
            }
            catch
            {
                var nameParts = blob.Uri.LocalPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return nameParts[1];
            }
        }

        public Task<IEnumerable<SubmittedBlob>> GetMySubmittedBlobsAsync(IPrincipal user, int pageSize = 30, int page = 0)
        {
            throw new NotImplementedException();
        }
    }
}
