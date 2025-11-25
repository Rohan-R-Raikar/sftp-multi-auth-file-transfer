using Renci.SshNet;
using SftpApi.Models;
using System.Text;

namespace SftpApi.Services
{
    public class SftpClientFactory
    {
        public SftpClient BuildClient(SftpAuthKey rec)
        {
            switch (rec.AuthType)
            {
                case AuthType.Password:
                    return new SftpClient(rec.Host, rec.Port, rec.Username, rec.Password);

                case AuthType.PrivateKeyNoPass:
                case AuthType.PrivateKeyWithPass:
                    return BuildPrivateKeyClient(rec);

                case AuthType.CertificateWithKey:
                    return BuildPrivateKeyClient(rec);

                default:
                    throw new InvalidOperationException(
                        $"Unknown authentication type: {rec.AuthType}");
            }
        }


        private SftpClient BuildPrivateKeyClient(SftpAuthKey rec)
        {
            if (string.IsNullOrWhiteSpace(rec.PrivateKeyBase64))
                throw new InvalidOperationException("Private key missing");

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(rec.PrivateKeyBase64);
            }
            catch
            {
                keyBytes = Encoding.UTF8.GetBytes(rec.PrivateKeyBase64);
            }

            using var ms = new MemoryStream(keyBytes);

            PrivateKeyFile pk;

            if (rec.AuthType == AuthType.PrivateKeyWithPass ||
                (rec.AuthType == AuthType.CertificateWithKey && !string.IsNullOrEmpty(rec.Passphrase)))
            {
                pk = new PrivateKeyFile(ms, rec.Passphrase);
            }
            else
            {
                pk = new PrivateKeyFile(ms);
            }

            var auth = new PrivateKeyAuthenticationMethod(rec.Username, pk);

            var conn = new Renci.SshNet.ConnectionInfo(
                rec.Host,
                rec.Port,
                rec.Username,
                auth
            );

            return new SftpClient(conn);
        }
    }
}
