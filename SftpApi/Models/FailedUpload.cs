namespace SftpApi.Models
{
    public class FailedUpload
    {
        public int Id { get; set; }
        public int SftpAuthKeyId { get; set; }

        public int? UserId { get; set; } = null;

        public string LocalFilePath { get; set; }
        public string RemotePath { get; set; }

        public DateTime FailedAt { get; set; }
        public bool IsRetried { get; set; }
    }
}
