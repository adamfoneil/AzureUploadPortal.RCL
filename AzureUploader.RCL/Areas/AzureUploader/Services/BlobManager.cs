using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AzureUploader.RCL.Areas.AzureUploader.Services
{
    public abstract class BlobManager
    {
        protected readonly StorageCredentials StorageCredentials;

        public BlobManager(StorageCredentials storageCredentials)
        {
            StorageCredentials = storageCredentials;
        }

        protected abstract Task<CloudBlobContainer> GetContainerAsync();

        protected virtual string GetBlobName(IFormFile file) => file.FileName;

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

        public async Task<IActionResult> UploadAsync(HttpRequest request, IPrincipal user)
        {
            var container = await GetContainerAsync();

            foreach (var file in request.Form.Files)
            {
                using (var stream = file.OpenReadStream())
                {
                    string blobName = GetBlobName(file);
                    var blob = container.GetBlockBlobReference(blobName);
                    blob.Properties.ContentType = file.ContentType;
                    await blob.UploadFromStreamAsync(stream);
                    await OnBlobUploaded(user, blob);
                }
            }

            return new OkResult();
        }

        public async Task<IEnumerable<CloudBlockBlob>> GetMyBlobsAsync(IPrincipal user)
        {
            var container = await GetContainerAsync();

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
    }
}
