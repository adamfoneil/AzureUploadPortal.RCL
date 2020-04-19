using AzureUploader.RCL.Areas.AzureUploader.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SampleApp.Services;
using System.Threading.Tasks;

namespace SampleApp.Pages
{
    public class IndexModel : PageModel
    {
        public IndexModel(BlobManager blobManager)
        {
            BlobManager = blobManager;
        }

        public BlobManager BlobManager { get; }

        public void OnGet()
        {
        }

        public async Task<RedirectResult> OnPostCreateDirAsync(string path)
        {
            await BlobManager.CreateFolderAsync(User, path);
            return Redirect("/Index");
        }
    }
}
