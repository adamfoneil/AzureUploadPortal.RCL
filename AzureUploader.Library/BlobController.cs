using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage.Blob;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AzureUploader.Library
{
    public abstract class BlobController : Controller
    {
        protected abstract Task<CloudBlobContainer> GetContainerAsync();

        protected virtual string GetBlobName(IFormFile file) => file.FileName;

        protected virtual Task OnBlobUploaded(IPrincipal user, CloudBlockBlob blob) => Task.CompletedTask;

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Upload()
        {
            var container = await GetContainerAsync();

            foreach (var file in Request.Form.Files)
            {
                using (var stream = file.OpenReadStream())
                {
                    string blobName = GetBlobName(file);
                    var blob = container.GetBlockBlobReference(blobName);
                    await blob.UploadFromStreamAsync(stream);
                    await OnBlobUploaded(User, blob);
                }
            }            

            return Ok();
        }
    }
}
