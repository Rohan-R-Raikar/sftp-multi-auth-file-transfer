using SftpApi.Data;
using SftpApi.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.IO;

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
            _logger.LogInformation("=== RetryUpload START ===");
            _logger.LogInformation("Retry request received | Id: {Id}, LocalFile: {LocalFile}, RemotePath: {RemotePath}",
                id, localFilePath, remotePath);

            var rec = _db.SftpAuthKeys.Find(id);
            if (rec == null)
            {
                _logger.LogWarning("Retry failed: No SFTP record found for ID {Id}", id);
                return;
            }

            _logger.LogDebug("Found SFTP record: Username={User}, Host={Host}, Port={Port}",
                rec.Username, rec.Host, rec.Port);

            if (!File.Exists(localFilePath))
            {
                _logger.LogWarning("Retry failed: Local file missing {File}", localFilePath);
                return;
            }

            _logger.LogInformation("Local file exists. Size={Length} bytes",
                new FileInfo(localFilePath).Length);

            try
            {
                _logger.LogInformation("Creating SFTP client...");
                using var client = _factory.BuildClient(rec);

                _logger.LogInformation("Connecting to SFTP: {Host}:{Port} as {User}",
                    rec.Host, rec.Port, rec.Username);

                client.Connect();
                _logger.LogInformation("SFTP connected successfully.");

                string safeFolderName = $"sftpuser_{rec.Id}";
                string remoteUserFolder = $"/Users/{rec.Username}/Downloads/{safeFolderName}";

                _logger.LogInformation("Checking if remote directory exists: {Dir}", remoteUserFolder);

                if (!client.Exists(remoteUserFolder))
                {
                    _logger.LogWarning("Directory does not exist. Creating remote directory: {Dir}",
                        remoteUserFolder);

                    client.CreateDirectory(remoteUserFolder);
                    _logger.LogInformation("Remote directory created: {Dir}", remoteUserFolder);
                }
                else
                {
                    _logger.LogInformation("Remote directory already exists.");
                }

                string remoteFile = $"{remoteUserFolder}/{Path.GetFileName(localFilePath)}";

                _logger.LogInformation("Preparing to upload file...");
                _logger.LogDebug("Remote file path: {RemoteFile}", remoteFile);

                using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    _logger.LogInformation("Uploading file...");
                    client.UploadFile(fs, remoteFile);
                }

                _logger.LogInformation("File upload completed: {RemoteFile}", remoteFile);

                client.Disconnect();
                _logger.LogInformation("SFTP connection closed.");

                // Delete local file
                System.IO.File.Delete(localFilePath);
                _logger.LogInformation("Local file deleted after successful upload: {File}", localFilePath);

                _logger.LogInformation("Retry success: {File}", remoteFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry upload failed for file: {File}", localFilePath);
                throw;
            }

            _logger.LogInformation("=== RetryUpload END ===");
        }
    }
}
