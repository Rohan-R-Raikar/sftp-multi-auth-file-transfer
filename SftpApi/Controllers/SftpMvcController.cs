using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using SftpApi.Data;
using SftpApi.Models;

namespace SftpApi.Controllers
{
    public class SftpMvcController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SftpMvcController(ApplicationDbContext db)
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
            if (rec == null)
                return NotFound();

            return View(rec);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(int id, IFormFile file)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
                return NotFound();

            if (file == null)
            {
                TempData["error"] = "No file selected.";
                return RedirectToAction("Upload", new { id });
            }

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                using var client = BuildClient(rec);
                client.Connect();

                string remoteFile = client.WorkingDirectory + "/" + file.FileName;
                client.UploadFile(ms, remoteFile);
                client.Disconnect();

                string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string targetFolder = $@"C:\Users\sftpuser\Downloads\MvcUID{id}({time})";

                Directory.CreateDirectory(targetFolder);

                string localPath = Path.Combine(targetFolder, file.FileName);

                using (var fs = new FileStream(localPath, FileMode.Create))
                {
                    ms.Position = 0;
                    await ms.CopyToAsync(fs);
                }

                TempData["success"] =
                    $"Uploaded to SFTP: {remoteFile} AND saved locally: {localPath}";
            }
            catch (Exception ex)
            {
                TempData["error"] = "Upload failed: " + ex.Message;
            }

            return RedirectToAction("Upload", new { id });
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
