namespace SftpApi.Logger
{
    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _path;
        public SimpleFileLoggerProvider(string path) => _path = path;
        public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(_path);
        public void Dispose() { }
    }
}
