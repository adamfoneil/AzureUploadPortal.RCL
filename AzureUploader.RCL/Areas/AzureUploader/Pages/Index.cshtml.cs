using AzureUploader.RCL.Areas.AzureUploader.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Azure.Storage.Blob;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureUploader.RCL.Areas.AzureUploader.Pages
{
    public class IndexModel : PageModel
    {
        private readonly BlobManager _blobManager;

        public IndexModel(BlobManager blobManager)
        {
            _blobManager = blobManager;
        }

        public IEnumerable<CloudBlockBlob> MyBlobs { get; set; }

        public async Task OnGetAsync()
        {
            MyBlobs = await _blobManager.GetMyBlobsAsync(User);
        }

        [HttpPost]
        public async Task<RedirectResult> OnPostAsync()
        {
            await _blobManager.UploadAsync(Request, User);
            return Redirect("/AzureUploader");
        }
    }
}
