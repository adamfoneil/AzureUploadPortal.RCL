using AzureUploader.RCL.Areas.AzureUploader.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SampleApp.Services
{
    public class MyBlobManager : BlobManager
    {
        public MyBlobManager(IConfiguration config) : base(new StorageCredentials(config["StorageAccount:Name"], config["StorageAccount:Key"]))
        {
        }

        protected override async Task<CloudBlobContainer> GetContainerAsync() => await GetContainerInternalAsync("sample-uploads");

        protected override string GetBlobName(IPrincipal user, IFormFile file)
        {
            string userName = GetUserFolderName(user);
            return Path.Combine(userName, file.FileName);
        }
    }
}
