namespace SftpApi.Models.ViewModels
{
    public class SftpIndexViewModel
    {
        public SftpAuthKey AuthKey { get; set; }
        public List<FailedUpload> FailedJobs { get; set; }
    }

}
