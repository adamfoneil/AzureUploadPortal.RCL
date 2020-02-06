using System;

namespace AzureUploader.RCL.Models
{
    public class UploadedFile
    {
        public string Filename { get; set; }
        public long Length { get; set; }
        public DateTime Timestamp { get; set; }
        public string Url { get; set; }
    }
}
