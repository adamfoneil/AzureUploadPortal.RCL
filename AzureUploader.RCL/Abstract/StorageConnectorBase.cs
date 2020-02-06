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

        public StorageConnectorBase(StorageCredentials credentials)
        {
            _creds = credentials;
        }

        protected abstract string ContainerName { get; }

        protected abstract Task LogUserUploadAsync(string userName, UploadedFile uploadedFile);

        private async Task<CloudBlobContainer> GetContainerAsync()
        {
            var account = new CloudStorageAccount(_creds, true);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(ContainerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public async Task<IEnumerable<CloudBlockBlob>> GetUserBlobsAsync(string userName)
        {
            var container = await GetContainerAsync();

            List<CloudBlockBlob> results = new List<CloudBlockBlob>();

            BlobContinuationToken token = default;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(userName + "/", token);
                results.AddRange(segment.Results.Select(item => (CloudBlockBlob)item));
                token = segment.ContinuationToken;
            } while (token != null);

            return results;
        }        

        //public async Task<IEnumerable<IListBlobItem>>

        public async Task UploadUserBlobAsync(string userName, IFormFile file)
        {
            var container = await GetContainerAsync();

            var blob = container.GetBlockBlobReference(userName + "/" + file.FileName);
            blob.Properties.ContentType = file.ContentType;

            using (var stream = file.OpenReadStream())
            {
                await blob.UploadFromStreamAsync(stream);
            }

            await LogUserUploadAsync(userName, new UploadedFile()
            {
                Filename = file.FileName,
                Length = file.Length,
                Timestamp = DateTime.UtcNow,
                Url = blob.Uri.AbsoluteUri
            });
        }
    }
}
