using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using SftpApi.Data;
using SftpApi.Models;

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
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null) return NotFound();
            return View(rec);
        }

        // --------------------------
        // 1) Try to connect only
        // --------------------------
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

        // --------------------------
        // 2) List directories under a path (AJAX)
        // --------------------------
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
                                        // Detect if directory has children (cheap check)
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

        // --------------------------
        // 3) Upload file to selected remote path
        // --------------------------
        [HttpPost]
        public async Task<IActionResult> SaveToServer([FromForm] int id,[FromForm] string remotePath)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
                return Json(new { success = false, error = "Record not found" });

            if (Request.Form.Files.Count == 0)
                return Json(new { success = false, error = "No file uploaded" });

            var file = Request.Form.Files[0];

            try
            {
                //---------------------------------------------
                // 1️⃣ Prepare memory stream for upload
                //---------------------------------------------
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                //---------------------------------------------
                // 2️⃣ Upload to SFTP
                //---------------------------------------------
                using var client = BuildClient(rec);
                client.Connect();

                string normalized = remotePath.TrimEnd('/');
                string remoteFile = normalized + "/" + file.FileName;

                client.UploadFile(ms, remoteFile);
                client.Disconnect();

                //---------------------------------------------
                // 3️⃣ Save Locally ALSO
                //---------------------------------------------
                //string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                //string targetFolder = $@"C:\Users\sftpuser\Downloads\MvcUID{id}({time})";

                //Directory.CreateDirectory(targetFolder);

                //string localPath = Path.Combine(targetFolder, file.FileName);

                //// Write stream again to local file
                //ms.Position = 0;
                //using (var fs = new FileStream(localPath, FileMode.Create))
                //{
                //    await ms.CopyToAsync(fs);
                //}

                //---------------------------------------------
                // 4️⃣ Return result
                //---------------------------------------------
                return Json(new
                {
                    success = true,
                    remoteFile
                    //localPath
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }



        // --------------------------
        // BuildClient() same as before
        // --------------------------
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
