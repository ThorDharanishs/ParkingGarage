using System.Text;

namespace ParkingGarage.FileHelper
{
    public class AuditLogger
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public AuditLogger(string filePath)
        {
            _filePath = filePath;
        }

        public async Task LogAsync(string message, CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await using FileStream fileStream = new(
                    _filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await using StreamWriter writer = new(fileStream, new UTF8Encoding(false));
                await writer.WriteAsync(
                    $"{DateTime.Now:O} - {message}{Environment.NewLine}".AsMemory(),
                    cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
