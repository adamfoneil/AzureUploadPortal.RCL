using AzureUploader.RCL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureUploader.RCL.Abstract
{
    public abstract class StorageConnectorBase
    {
        private readonly StorageCredentials _creds;

        private const string mdUserName = "userName";
        private const string mdSourceFile = "sourceFile";

        public StorageConnectorBase(StorageCredentials credentials)
        {
            _creds = credentials;
        }

        protected abstract string ContainerName { get; }
        protected abstract Task<CloudBlockBlob> GetProcessDestinationBlobAsync(CloudBlockBlob sourceBlob);
        protected abstract Task<int> LogProcessStartedAsync(ProcessedBlob uploadedFile);
        protected abstract Task LogProcessDoneAsync(int id, bool successful, string message = null);

        private async Task<CloudBlobContainer> GetContainerAsync()
        {
            var account = new CloudStorageAccount(_creds, true);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(ContainerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        /// <summary>
        /// files uploaded by a user that haven't been processed yet
        /// </summary>
        public async Task<IEnumerable<CloudBlockBlob>> GetPendingBlobsAsync(string userName)
        {
            var container = await GetContainerAsync();

            List<CloudBlockBlob> results = new List<CloudBlockBlob>();

            BlobContinuationToken token = default;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(userName + "/", token);
                results.AddRange(segment.Results.OfType<CloudBlockBlob>());
                token = segment.ContinuationToken;
            } while (token != null);

            return results;
        }

        /// <summary>
        /// copies a blob from the upload area to a destination
        /// </summary>
        public async Task ProcessBlob(string blobUri)
        {
            var sourceBlob = new CloudBlockBlob(new Uri(blobUri), _creds);
            if (!(await sourceBlob.ExistsAsync())) throw new Exception($"Blob not found: {blobUri}");
            
            var destBlob = await GetProcessDestinationBlobAsync(sourceBlob);

            if (await destBlob.ExistsAsync()) throw new Exception($"Destination blob {destBlob.Uri} already exists.");
            if (sourceBlob.Uri.Equals(destBlob.Uri)) throw new InvalidOperationException("Source and destination blobs cannot be the same.");

            int logId = await LogProcessStartedAsync(new ProcessedBlob()
            {
                Timestamp = DateTime.UtcNow,
                UserName = sourceBlob.Metadata[mdUserName],
                Filename = sourceBlob.Metadata[mdSourceFile],
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

        /// <summary>
        /// gets the blobs processed for a user
        /// </summary>
        public abstract Task<IEnumerable<ProcessedBlob>> GetProcessedBlobsAsync(string userName, int pageSize = 30, int page = 0);

        /// <summary>
        /// receives an uploaded file from a user
        /// </summary>
        public async Task UploadUserBlobAsync(string userName, IFormFile file)
        {
            var container = await GetContainerAsync();

            var blob = container.GetBlockBlobReference(userName + "/" + file.FileName);
            blob.Properties.ContentType = file.ContentType;
            blob.Metadata.Add(mdUserName, userName);
            blob.Metadata.Add(mdSourceFile, file.FileName);
            blob.Metadata.Add("timestamp", DateTime.UtcNow.ToString());
            await SetMetadataAsync(userName, blob);

            using (var stream = file.OpenReadStream())
            {
                await blob.UploadFromStreamAsync(stream);                
            }

            await blob.SetMetadataAsync();
        }

        /// <summary>
        /// override this to capture any additional metadata useful on your uploads
        /// </summary>
        protected async Task SetMetadataAsync(string userName, CloudBlockBlob blob)
        {
            await Task.CompletedTask;
        }
    }
}
