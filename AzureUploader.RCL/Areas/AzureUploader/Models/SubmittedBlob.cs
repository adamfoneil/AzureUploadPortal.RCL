using System;
using System.ComponentModel.DataAnnotations;

namespace AzureUploader.RCL.Areas.AzureUploader.Models
{
    public class SubmittedBlob
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        [MaxLength(50)]
        public string UserName { get; set; }
        [MaxLength(255)]
        public string Path { get; set; }
        public long Length { get; set; }
        public bool IsOverwrite { get; set; }
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
    }
}
