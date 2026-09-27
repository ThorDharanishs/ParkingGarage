using System.Text;
using ParkingGarage.Core.Model;

namespace ParkingGarage.FileHelper
{
    public class CsvFileHandler
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _fileSemaphore = new(1, 1);

        public CsvFileHandler(string filePath)
        {
            _filePath = filePath;
        }

        public async Task AppendAsync(
            IReadOnlyList<CompletedStay> stays,
            CancellationToken cancellationToken)
        {
            if (stays.Count == 0)
            {
                return;
            }

            await _fileSemaphore.WaitAsync(cancellationToken);
            try
            {
                bool fileExists = File.Exists(_filePath);

                await using FileStream fileStream = new(
                    _filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await using StreamWriter writer = new(fileStream, new UTF8Encoding(false));

                if (!fileExists || fileStream.Length == 0)
                {
                    await writer.WriteAsync(
                        "TicketId,Plate,Type,Level,Spot,EntryTime,ExitTime,DurationMinutes,Fee".AsMemory(),
                        cancellationToken);
                    await writer.WriteAsync(Environment.NewLine.AsMemory(), cancellationToken);
                }

                foreach (CompletedStay stay in stays)
                {
                    string line = string.Join(",",
                        Escape(stay.TicketId),
                        Escape(stay.Plate),
                        Escape(stay.Type.ToString()),
                        Escape(stay.Level.ToString()),
                        Escape(stay.Spot),
                        Escape(stay.EntryTime.ToString("O")),
                        Escape(stay.ExitTime.ToString("O")),
                        Escape(stay.DurationMinutes.ToString()),
                        Escape(stay.Fee.ToString("0.00")));

                    await writer.WriteAsync(line.AsMemory(), cancellationToken);
                    await writer.WriteAsync(Environment.NewLine.AsMemory(), cancellationToken);
                }

                await writer.FlushAsync(cancellationToken);
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        private static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
