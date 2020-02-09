using AzureUploader.RCL.Areas.AzureUploader.Models;
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

        protected override async Task<CloudBlobContainer> GetPortalContainerAsync() => await GetContainerInternalAsync("sample-uploads");

        protected override Task<CloudBlobContainer> GetProcessingContainerAsync(string userName)
        {
            throw new System.NotImplementedException();
        }

        protected override string GetBlobName(IPrincipal user, IFormFile file)
        {
            string userName = GetUserFolderName(user);
            return Path.Combine(userName, file.FileName);
        }

        protected override Task LogProcessDoneAsync(int id, bool successful, string message = null)
        {
            throw new System.NotImplementedException();
        }

        protected override Task<int> LogProcessStartedAsync(ProcessedBlob processedBlob)
        {
            throw new System.NotImplementedException();
        }
    }
}
