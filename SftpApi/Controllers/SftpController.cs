using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using SftpApi.Data;
using SftpApi.Models;
using SftpApi.Models.DTOs;

namespace SftpApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SftpController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public SftpController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("transfer/{id}")]
        public async Task<IActionResult> Transfer(int id)
        {
            var rec = await _db.SftpAuthKeys.FindAsync(id);
            if (rec == null)
                return NotFound(new { success = false, error = "Record not found" });

            try
            {
                using var client = BuildClient(rec);

                client.Connect();

                string sourceFile = @"D:\New\New Text Document.txt";
                if (!System.IO.File.Exists(sourceFile))
                    return BadRequest(new { success = false, error = "Source file not found: " + sourceFile });

                string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string targetFolder = $@"C:\Newfolder\UID{id}({time})";
                Directory.CreateDirectory(targetFolder);

                string targetFile = Path.Combine(targetFolder, "New Text Document.txt");

                System.IO.File.Copy(sourceFile, targetFile, overwrite: true);

                string infoFile = Path.Combine(targetFolder, "TransferInfo.txt");
                string infoContent = $@"
                                        Transfer Timestamp: {time}
                                        User/Record ID: {id}
                                        Source File: {sourceFile}
                                        Target File: {targetFile}
                                        AuthType Used: {rec.AuthType}
                                        Host: {rec.Host}
                                        Port: {rec.Port}
                                        Username: {rec.Username}
                                        ";
                System.IO.File.WriteAllText(infoFile, infoContent);

                client.Disconnect();

                return Ok(new
                {
                    success = true,
                    message = "Handshake OK, file copied successfully",
                    saved_to = targetFile,
                    info_file = infoFile
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
        private SftpClient BuildClient(SftpAuthKey rec)
        {
            if (rec.AuthType == AuthType.Password)
            {
                if (string.IsNullOrEmpty(rec.Password))
                    throw new InvalidOperationException("Password missing");

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
                if (string.IsNullOrEmpty(rec.Passphrase))
                    throw new InvalidOperationException("Passphrase missing");

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

        private string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ".";

            if (path.Contains(":"))
                path = "/" + path.Replace("\\", "/").Replace(":", "");

            return path;
        }
    }
}
