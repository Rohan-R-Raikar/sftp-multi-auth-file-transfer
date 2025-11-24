namespace SftpApi.Models.DTOs
{
    public class SftpKeyDto
    {
        public string? PrivateKeyBase64 { get; set; }
        public string? Passphrase { get; set; }

        public string Host { get; set; }
        public int Port { get; set; } = 22;
        public string Username { get; set; }
    }
}
