using AzureUploader.RCL.Areas.AzureUploader.Models;
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

        public IEnumerable<CloudBlockBlob> MyUploads { get; set; }
        public IEnumerable<SubmittedBlob> SubmittedBlobs { get; set; }

        public async Task OnGetAsync()
        {
            MyUploads = await _blobManager.GetMyBlobsAsync(User);
            SubmittedBlobs = await _blobManager.GetMySubmittedBlobsAsync(User);
        }

        [HttpPost]
        public async Task<RedirectResult> OnPostAsync()
        {
            await _blobManager.UploadAsync(Request, User);
            return Redirect("/AzureUploader");
        }

        [HttpPost]
        public async Task<RedirectResult> OnPostSubmitAsync([FromForm]string uri)
        {
            await _blobManager.SubmitBlobAsync(uri);
            return Redirect("/AzureUploader");
        }
    }
}
