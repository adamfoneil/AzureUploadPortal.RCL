using AzureUploader.RCL.Areas.AzureUploader.Models;
using AzureUploader.RCL.Areas.AzureUploader.Services;
using Dapper.CX.Classes;
using Dapper.CX.SqlServer.Extensions.Int;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SampleApp.Queries;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SampleApp.Services
{
    public class MyBlobManager : BlobManager
    {
        private readonly string _connectionString;

        public MyBlobManager(IConfiguration config) : base(new StorageCredentials(config["StorageAccount:Name"], config["StorageAccount:Key"]))
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        protected override string UploadContainerName => "sample-uploads";

        protected override Task<CloudBlobContainer> GetSubmittedContainerAsync(string userName) => GetContainerInternalAsync("submitted");
        
        protected override string GetBlobName(IPrincipal user, IFormFile file)
        {
            string userName = GetUserFolderName(user);
            return Path.Combine(userName, file.FileName);
        }

        protected override async Task LogSubmitDoneAsync(int id, bool successful, string message = null)
        {
            using (var cn = GetConnection())
            {
                var log = await cn.GetAsync<SubmittedBlob>(id);
                var ct = new ChangeTracker<SubmittedBlob>(log);
                log.IsSuccessful = successful;
                log.ErrorMessage = message;
                await cn.SaveAsync(log, ct);
            }
        }

        protected override async Task<int> LogSubmitStartedAsync(SubmittedBlob submittedBlob)
        {
            using (var cn = GetConnection())
            {
                return await cn.SaveAsync(submittedBlob);
            }
        }

        protected override async Task<IEnumerable<SubmittedBlob>> QuerySubmittedBlobsAsync(string userName, int pageSize = 30, int page = 0)
        {
            using (var cn = GetConnection())
            {
                return await new MySubmittedBlobs() { UserName = userName, Page = page }.ExecuteAsync(cn);
            }
        }

        /*
        CREATE TABLE [dbo].[SubmittedBlob] (
            [Id] int identity(1,1),
            [Timestamp] datetime NOT NULL,
            [UserName] nvarchar(50) NOT NULL,
            [Path] nvarchar(255) NOT NULL,
            [Length] bigint NOT NULL,
            [IsOverwrite] bit NOT NULL,
            [IsSuccessful] bit NOT NULL,
            [ErrorMessage] nvarchar(max) NULL,
            CONSTRAINT [PK_SubmittedBlob] PRIMARY KEY ([Id])
        )
        */
    }
}
