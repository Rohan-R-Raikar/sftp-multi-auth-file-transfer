using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using SftpApi.Data;
using SftpApi.Models;
using SftpApi.Models.ViewModels;
using SftpApi.Services;

namespace SftpApi.Controllers
{
    public class SftpMvcCustomController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SftpMvcCustomController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.SftpAuthKeys.ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Upload(int id)
        {
            var vm = new SftpIndexViewModel
            {
                AuthKey = await _db.SftpAuthKeys.FindAsync(id),
                FailedJobs = await _db.FailedUploads.Where(x => x.SftpAuthKeyId == id && x.IsRetried == false).ToListAsync()
            };
            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> TryConnect(int id)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null) return Json(new { success = false, error = "Record not found" });

            try
            {
                using var client = BuildClient(rec);
                client.Connect();
                string startPath = client.WorkingDirectory ?? "/";
                client.Disconnect();
                return Json(new { success = true, startPath });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDirectories(int id, string path)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null) return Json(new { success = false, error = "Record not found" });

            try
            {
                using var client = BuildClient(rec);
                client.Connect();

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

                client.Disconnect();

                return Json(new { success = true, entries });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveToServer([FromForm] int id, [FromForm] string remotePath)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
                return Json(new { success = false, error = "Record not found" });

            if (Request.Form.Files.Count == 0)
                return Json(new { success = false, error = "No file uploaded" });

            var uploadedFile = Request.Form.Files[0];

            string safeFolderName = $"{rec.Username}_{rec.Id}";

            string tempBase = @"C:\TempSftpUploads";

            string userTempFolder = Path.Combine(tempBase, safeFolderName);
            Directory.CreateDirectory(userTempFolder);

            string localFilePath = Path.Combine(userTempFolder, uploadedFile.FileName);

            using (var fs = new FileStream(localFilePath, FileMode.Create))
                await uploadedFile.CopyToAsync(fs);

            try
            {
                using var client = BuildClient(rec);
                client.Connect();

                string remoteUserFolder = $"{remotePath.TrimEnd('/')}/{safeFolderName}";
                if (!client.Exists(remoteUserFolder))
                    client.CreateDirectory(remoteUserFolder);

                string remoteFile = $"{remoteUserFolder}/{uploadedFile.FileName}";

                using (var fs2 = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    client.UploadFile(fs2, remoteFile);
                }

                client.Disconnect();

                System.IO.File.Delete(localFilePath);

                return Json(new { success = true, message = "Uploaded successfully.", remoteFile });
            }
            catch
            {
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
            var fail = _db.FailedUploads.Find(failedId);
            if (fail == null)
                return Json(new { success = false, error = "Entry not found" });

            Hangfire.BackgroundJob.Enqueue<SftpRetryService>(x =>
                x.RetryUpload(fail.SftpAuthKeyId, fail.LocalFilePath, fail.RemotePath)
            );

            fail.IsRetried = true;
            _db.SaveChanges();

            return Json(new { success = true, message = "Retry queued in Hangfire" });
        }


        private SftpClient BuildClient(SftpAuthKey rec)
        {
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
            {
                pk = new PrivateKeyFile(ms, rec.Passphrase);
            }
            else
            {
                pk = new PrivateKeyFile(ms);
            }

            var auth = new PrivateKeyAuthenticationMethod(rec.Username, pk);
            var conn = new Renci.SshNet.ConnectionInfo(rec.Host, rec.Port, rec.Username, auth);

            return new SftpClient(conn);
        }
    }
}
