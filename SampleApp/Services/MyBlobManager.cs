using AzureUploader.RCL.Areas.AzureUploader.Services;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace SampleApp.Services
{
    public class MyBlobManager : BlobManager
    {
        public MyBlobManager(IConfiguration config) : base(new StorageCredentials(config["StorageAccount:Name"], config["StorageAccount:Key"]))
        {

        }
        protected override async Task<CloudBlobContainer> GetContainerAsync() => await GetContainerInternalAsync("sample-uploads");
        
    }
}
