using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SftpApi.Models
{
    public enum AuthType : byte
    {
        Password = 1,
        PrivateKeyNoPass = 2,
        PrivateKeyWithPass = 3,
        CertificateWithKey = 4
    }


    public class SftpAuthKey
    {
        public int Id { get; set; }
        public AuthType AuthType { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 22;
        public string Username { get; set; }

        public string? PrivateKeyBase64 { get; set; }
        public string? PublicKeyBase64 { get; set; }
        public string? CertificateBase64 { get; set; }

        public string? Password { get; set; }

        public string? Passphrase { get; set; }

        public string? Description { get; set; }
    }
}
