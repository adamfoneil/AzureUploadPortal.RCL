using System;

namespace AzureUploader.RCL.Models
{
    public class ProcessedBlob
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; }
        public string Filename { get; set; }
        public long Length { get; set; }        
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
    }
}
