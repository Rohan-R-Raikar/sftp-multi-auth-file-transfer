using SftpApi.Data;
using SftpApi.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SftpApi.Services
{
    public class SftpRetryService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SftpRetryService> _logger;
        private readonly SftpClientFactory _factory;

        public SftpRetryService(
            ApplicationDbContext db,
            ILogger<SftpRetryService> logger,
            SftpClientFactory factory)
        {
            _db = db;
            _logger = logger;
            _factory = factory;
        }

        public void RetryUpload(int id, string localFilePath, string remotePath)
        {
            var rec = _db.SftpAuthKeys.Find(id);
            if (rec == null)
            {
                _logger.LogWarning("Retry failed: No SFTP record found for ID {Id}", id);
                return;
            }

            if (!File.Exists(localFilePath))
            {
                _logger.LogWarning("Retry failed: Local file missing {File}", localFilePath);
                return;
            }

            try
            {
                using var client = _factory.BuildClient(rec);
                client.Connect();

                string safeFolderName = $"{rec.Username}_{rec.Id}";
                string remoteUserFolder = $"{remotePath.TrimEnd('/')}/{safeFolderName}";

                if (!client.Exists(remoteUserFolder))
                    client.CreateDirectory(remoteUserFolder);

                string remoteFile = $"{remoteUserFolder}/{Path.GetFileName(localFilePath)}";

                // IMPORTANT FIX: The file stream MUST be inside using
                using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    client.UploadFile(fs, remoteFile);
                }

                client.Disconnect();

                // Now safe to delete because fs is closed
                File.Delete(localFilePath);

                _logger.LogInformation("Retry success: {File}", remoteFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry upload failed for file: {File}", localFilePath);
                throw;
            }
        }

    }
}
