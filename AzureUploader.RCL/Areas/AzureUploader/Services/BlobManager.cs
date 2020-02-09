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
        protected abstract Task<CloudBlobContainer> GetPortalContainerAsync();

        /// <summary>
        /// where do uploads go when "processed"?
        /// </summary>
        protected abstract Task<CloudBlobContainer> GetProcessingContainerAsync(string userName);

        protected abstract Task<int> LogProcessStartedAsync(ProcessedBlob processedBlob);

        protected abstract Task LogProcessDoneAsync(int id, bool successful, string message = null);

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
            var container = await GetPortalContainerAsync();

            foreach (var file in request.Form.Files)
            {
                using (var stream = file.OpenReadStream())
                {
                    string blobName = GetBlobName(user, file);
                    var blob = container.GetBlockBlobReference(blobName);
                    blob.Properties.ContentType = file.ContentType;
                    blob.Metadata.Add(mdUserName, GetUserFolderName(user));
                    blob.Metadata.Add(mdSourceFile, file.FileName);
                    blob.Metadata.Add("timestamp", DateTime.UtcNow.ToString());

                    await blob.UploadFromStreamAsync(stream);
                    await OnBlobUploaded(user, blob);
                }
            }
        }

        public async Task<IEnumerable<CloudBlockBlob>> GetMyBlobsAsync(IPrincipal user)
        {
            var container = await GetPortalContainerAsync();

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

        public async Task ProcessBlobAsync(string blobUri)
        {
            var sourceUri = new Uri(blobUri);
            var sourceBlob = new CloudBlockBlob(sourceUri);
            if (!(await sourceBlob.ExistsAsync())) throw new Exception($"Source blob {blobUri} not found.");

            var nameParts = sourceUri.LocalPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string userName = nameParts.First();
            string destName = string.Join('/', nameParts.Skip(1));
            var container = await GetProcessingContainerAsync(userName);
            var destBlob = container.GetBlockBlobReference(destName);            

            if (await destBlob.ExistsAsync()) throw new Exception($"Destination blob {destBlob.Uri} already exists.");
            if (sourceBlob.Uri.Equals(destBlob.Uri)) throw new InvalidOperationException("Source and destination blobs cannot be the same.");

            int logId = await LogProcessStartedAsync(new ProcessedBlob()
            {
                Timestamp = DateTime.UtcNow,
                UserName = sourceBlob.Metadata[mdUserName],
                Path = sourceBlob.Metadata[mdSourceFile],
                Length = sourceBlob.Properties.Length
            });

            try
            {
                using (var source = sourceBlob.OpenRead())
                {
                    await destBlob.UploadFromStreamAsync(source);
                }

                await sourceBlob.DeleteAsync();

                await LogProcessDoneAsync(logId, true);
            }
            catch (Exception exc)
            {
                await LogProcessDoneAsync(logId, false, exc.Message);
            }
        }

        public Task<IEnumerable<ProcessedBlob>> GetMyUploadHistoryAsync(string userName, int pageSize = 30, int page = 0)
        {
            throw new NotImplementedException();
        }
    }
}
