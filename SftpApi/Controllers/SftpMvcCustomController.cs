using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using SftpApi.Data;
using SftpApi.Models;
using SftpApi.Models.ViewModels;
using SftpApi.Services;
using Microsoft.Extensions.Logging;

namespace SftpApi.Controllers
{
    public class SftpMvcCustomController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SftpMvcCustomController> _logger;

        public SftpMvcCustomController(ApplicationDbContext db, ILogger<SftpMvcCustomController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("=== SftpMvcCustomController.Index START ===");
            var list = await _db.SftpAuthKeys.ToListAsync();
            _logger.LogInformation("Loaded {Count} SFTP Auth Records", list.Count);
            _logger.LogInformation("=== SftpMvcCustomController.Index END ===");
            return View(list);
        }

        public async Task<IActionResult> Upload(int id)
        {
            _logger.LogInformation("Opening Upload screen for AuthKey ID: {Id}", id);

            var vm = new SftpIndexViewModel
            {
                AuthKey = await _db.SftpAuthKeys.FindAsync(id),
                FailedJobs = await _db.FailedUploads
                                .Where(x => x.SftpAuthKeyId == id && x.IsRetried == false)
                                .ToListAsync()
            };

            _logger.LogInformation("Loaded Upload View | Failed Jobs: {Count}", vm.FailedJobs.Count);
            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> TryConnect(int id)
        {
            _logger.LogInformation("TryConnect called for Id={Id}", id);

            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
            {
                _logger.LogWarning("TryConnect failed. AuthKey not found: {Id}", id);
                return Json(new { success = false, error = "Record not found" });
            }

            try
            {
                using var client = BuildClient(rec);
                _logger.LogInformation("Connecting to SFTP {Host}:{Port}", rec.Host, rec.Port);

                client.Connect();
                string startPath = client.WorkingDirectory ?? "/";

                _logger.LogInformation("Connected successfully. WorkingDir={Path}", startPath);

                client.Disconnect();
                return Json(new { success = true, startPath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryConnect failed for Id={Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDirectories(int id, string path)
        {
            _logger.LogInformation("GetDirectories called | Id={Id}, Path={Path}", id, path);

            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
            {
                _logger.LogWarning("GetDirectories failed. AuthKey not found: {Id}", id);
                return Json(new { success = false, error = "Record not found" });
            }

            try
            {
                using var client = BuildClient(rec);
                client.Connect();
                _logger.LogInformation("Connected to SFTP to list directories.");

                var entries = client.ListDirectory(path)
                                    .Where(e => e.IsDirectory && e.Name != "." && e.Name != "..")
                                    .Select(e => new
                                    {
                                        name = e.Name,
                                        fullPath = (path.TrimEnd('/') + "/" + e.Name).Replace("//", "/"),
                                        hasChildren = client.ListDirectory((path.TrimEnd('/') + "/" + e.Name).Replace("//", "/"))
                                                            .Any(x => x.IsDirectory && x.Name != "." && x.Name != "..")
                                    })
                                    .ToList();

                _logger.LogInformation("Found {Count} directories under {Path}", entries.Count, path);

                client.Disconnect();

                return Json(new { success = true, entries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDirectories failed for Id={Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveToServer([FromForm] int id, [FromForm] string remotePath)
        {
            _logger.LogInformation("SaveToServer called | Id={Id}, RemotePath={RemotePath}", id, remotePath);

            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
            {
                _logger.LogWarning("SaveToServer failed. Record not found: {Id}", id);
                return Json(new { success = false, error = "Record not found" });
            }

            if (Request.Form.Files.Count == 0)
            {
                _logger.LogWarning("SaveToServer failed. No file uploaded.");
                return Json(new { success = false, error = "No file uploaded" });
            }

            var uploadedFile = Request.Form.Files[0];
            _logger.LogInformation("File received: {Name} ({Size} bytes)", uploadedFile.FileName, uploadedFile.Length);

            string safeFolderName = $"{rec.Username}_{rec.Id}";
            string tempBase = @"C:\TempSftpUploads";

            string userTempFolder = Path.Combine(tempBase, safeFolderName);
            Directory.CreateDirectory(userTempFolder);

            string localFilePath = Path.Combine(userTempFolder, uploadedFile.FileName);

            using (var fs = new FileStream(localFilePath, FileMode.Create))
                await uploadedFile.CopyToAsync(fs);

            _logger.LogInformation("Saved temporary file: {LocalFilePath}", localFilePath);

            try
            {
                using var client = BuildClient(rec);

                client.Connect();
                _logger.LogInformation("Connected to SFTP to upload file.");

                string remoteUserFolder = $"{remotePath.TrimEnd('/')}/{safeFolderName}";

                if (!client.Exists(remoteUserFolder))
                {
                    _logger.LogInformation("Remote directory not found. Creating: {Dir}", remoteUserFolder);
                    client.CreateDirectory(remoteUserFolder);
                }

                string remoteFile = $"{remoteUserFolder}/{uploadedFile.FileName}";
                _logger.LogInformation("Uploading to: {RemoteFile}", remoteFile);

                using (var fs2 = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    client.UploadFile(fs2, remoteFile);
                }

                client.Disconnect();
                _logger.LogInformation("Upload complete. Removing local file.");

                System.IO.File.Delete(localFilePath);

                return Json(new { success = true, message = "Uploaded successfully.", remoteFile });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveToServer failed. Scheduling retry.");

                Hangfire.BackgroundJob.Enqueue<SftpRetryService>(x =>
                    x.RetryUpload(id, localFilePath, remotePath)
                );

                var fail = new FailedUpload
                {
                    SftpAuthKeyId = id,
                    LocalFilePath = localFilePath,
                    RemotePath = remotePath,
                    FailedAt = DateTime.UtcNow,
                    IsRetried = false
                };

                _db.FailedUploads.Add(fail);
                await _db.SaveChangesAsync();

                _logger.LogWarning("Failed upload recorded. FailId={FailId}", fail.Id);

                return Json(new
                {
                    success = false,
                    message = "Upload failed. Retry scheduled.",
                    failedId = fail.Id
                });
            }
        }

        [HttpPost]
        public IActionResult RetryNow(int failedId)
        {
            _logger.LogInformation("RetryNow called for FailedId={FailedId}", failedId);

            var fail = _db.FailedUploads.Find(failedId);
            if (fail == null)
            {
                _logger.LogWarning("RetryNow failed. Entry not found: {FailedId}", failedId);
                return Json(new { success = false, error = "Entry not found" });
            }

            Hangfire.BackgroundJob.Enqueue<SftpRetryService>(x =>
                x.RetryUpload(fail.SftpAuthKeyId, fail.LocalFilePath, fail.RemotePath)
            );

            fail.IsRetried = true;
            _db.SaveChanges();

            _logger.LogInformation("Retry queued in Hangfire for FailedId={FailedId}", failedId);

            return Json(new { success = true, message = "Retry queued in Hangfire" });
        }


        private SftpClient BuildClient(SftpAuthKey rec)
        {
            _logger.LogDebug("Building SFTP client for {User}@{Host}:{Port}", rec.Username, rec.Host, rec.Port);

            if (rec.AuthType == AuthType.Password)
            {
                return new SftpClient(rec.Host, rec.Port, rec.Username, rec.Password);
            }

            if (string.IsNullOrEmpty(rec.PrivateKeyBase64))
                throw new InvalidOperationException("Private key missing");

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(rec.PrivateKeyBase64);
            }
            catch
            {
                keyBytes = System.Text.Encoding.UTF8.GetBytes(rec.PrivateKeyBase64);
            }

            var ms = new MemoryStream(keyBytes);

            PrivateKeyFile pk;

            if (rec.AuthType == AuthType.PrivateKeyWithPass)
                pk = new PrivateKeyFile(ms, rec.Passphrase);
            else
                pk = new PrivateKeyFile(ms);

            var auth = new PrivateKeyAuthenticationMethod(rec.Username, pk);
            var conn = new Renci.SshNet.ConnectionInfo(rec.Host, rec.Port, rec.Username, auth);

            _logger.LogDebug("SFTP Client created successfully.");

            return new SftpClient(conn);
        }
    }
}
