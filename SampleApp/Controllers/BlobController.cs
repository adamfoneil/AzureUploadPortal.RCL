using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace SampleApp.Controllers
{
    [Authorize]
    public class BlobController : AzureUploader.Library.BlobController
    {        
        public BlobController(IConfiguration config) : base(new StorageCredentials(config["StorageAccount:Name"], config["StorageAccount:Key"]))
        {
        }

        protected override async Task<CloudBlobContainer> GetContainerAsync() => await GetContainerInternalAsync("sample-uploads");

        protected override string GetBlobName(IFormFile file)
        {            
            return Path.Combine(User.Identity.Name, file.FileName);
        }               
    }
}