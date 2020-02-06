using Microsoft.AspNetCore.Components;
using Microsoft.Azure.Storage.Auth;

namespace AzureUploader.RCL
{
    public class FileDropperBase : ComponentBase
    {
        [Inject]
        protected StorageCredentials StorageCredentials { get; set; }

        public FileDropperBase()
        {
        }
    }
}
