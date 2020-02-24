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

        public string TrimUserName(string path) => _blobManager.TrimUserName(User.Identity.Name, path);

        public string GetDownloadUrl(string blobUri) => _blobManager.GetDownloadUrl(blobUri);

        public async Task OnGetAsync()
        {
            MyUploads = await _blobManager.GetMyBlobsAsync(User);
            SubmittedBlobs = await _blobManager.GetMySubmittedBlobsAsync(User);
        }

        [HttpPost]
        public async Task<RedirectResult> OnPostAsync()
        {
            await _blobManager.UploadAsync(Request, User);
            return Redirect("/UploadPortal");
        }

        [HttpPost]
        public async Task<RedirectResult> OnPostSubmitAllAsync()
        {
            var blobs = await _blobManager.GetMyBlobsAsync(User);
            foreach (var blob in blobs) await _blobManager.SubmitBlobAsync(blob);
            return Redirect("/UploadPortal");
        }
    }
}
