using AzureUploader.RCL.Areas.AzureUploader.Models;
using Dapper.QX;
using Dapper.QX.Attributes;

namespace SampleApp.Queries
{
    public class MySubmittedBlobs : Query<SubmittedBlob>
    {
        public MySubmittedBlobs(int timeZoneOffset = 0) : base(
            $@"SELECT [sb].*, DATEADD(hh, {timeZoneOffset}, [Timestamp]) AS [LocalTime]
            FROM [dbo].[SubmittedBlob] [sb]
            WHERE [UserName]=@userName
            ORDER BY [Timestamp] DESC
            {{offset}}")
        {
        }

        public string UserName { get; set; }

        [Offset(30)]
        public int? Page { get; set; }
    }
}
