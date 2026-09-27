namespace ParkingGarage.Helper
{
    public class InputHelper
    {
        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task<string?> readTask = Console.In.ReadLineAsync();
            Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            Task completedTask = await Task.WhenAny(readTask, cancellationTask);

            if (completedTask == cancellationTask)
            {
                return null;
            }

            return await readTask;
        }

        public async Task<string?> ReadRequiredAsync(string message, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Write(message);
                string? input = await ReadLineAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    return input.Trim();
                }

                Console.WriteLine("Input cannot be empty. Please try again.");
            }

            return null;
        }
    }
}
