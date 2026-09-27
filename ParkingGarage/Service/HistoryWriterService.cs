using System.Threading.Channels;
using ParkingGarage.Core.Model;
using ParkingGarage.FileHelper;

namespace ParkingGarage.Service
{
    public class HistoryWriterService
    {
        private readonly Channel<CompletedStay> _channel;
        private readonly CsvFileHandler _csvFileHandler;
        private readonly FileHelper.AuditLogger _auditLogger;
        private readonly CancellationTokenSource _writerCancellationTokenSource = new();
        private Task? _writerTask;
        private int _pendingRows;
        private Exception? _writerException;

        public HistoryWriterService(string csvPath, FileHelper.AuditLogger auditLogger, int capacity = 200)
        {
            _channel = Channel.CreateBounded<CompletedStay>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            _csvFileHandler = new CsvFileHandler(csvPath);
            _auditLogger = auditLogger;
        }

        public int PendingRows => Volatile.Read(ref _pendingRows);
        public bool HasFaulted => _writerException != null;
        public string? FaultMessage => _writerException?.Message;

        public void Start(CancellationToken applicationCancellationToken)
        {
            _writerTask = RunAsync(applicationCancellationToken, _writerCancellationTokenSource.Token);
        }

        public async Task QueueAsync(CompletedStay completedStay, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _pendingRows);

            try
            {
                await _channel.Writer.WriteAsync(completedStay, cancellationToken);
            }
            catch
            {
                Interlocked.Decrement(ref _pendingRows);
                throw;
            }
        }

        public void Complete()
        {
            _channel.Writer.TryComplete();
        }

        public async Task CompleteAndDrainAsync()
        {
            _channel.Writer.TryComplete();

            if (_writerTask != null)
            {
                try
                {
                    await _writerTask;
                }
                catch (OperationCanceledException)
                {
                    // The application is already shutting down.
                }
            }
        }

        public async Task StopAsync()
        {
            _writerCancellationTokenSource.Cancel();

            if (_writerTask != null)
            {
                try
                {
                    await _writerTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }

            _writerCancellationTokenSource.Dispose();
        }

        private async Task RunAsync(
            CancellationToken applicationCancellationToken,
            CancellationToken writerCancellationToken)
        {
            try
            {
                using CancellationTokenSource linkedCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        applicationCancellationToken,
                        writerCancellationToken);

                CancellationToken token = linkedCancellationTokenSource.Token;

                await foreach (CompletedStay firstStay in _channel.Reader.ReadAllAsync(token))
                {
                    List<CompletedStay> batch = new() { firstStay };
                    Interlocked.Decrement(ref _pendingRows);

                    await FillBatchAsync(batch, token);

                    try
                    {
                        await _csvFileHandler.AppendAsync(batch, token);
                    }
                    catch (OperationCanceledException)
                    {
                        foreach (CompletedStay stay in batch)
                        {
                            Interlocked.Increment(ref _pendingRows);
                            await _channel.Writer.WriteAsync(stay, CancellationToken.None);
                        }

                        throw;
                    }
                    catch (Exception exception)
                    {
                        _writerException = exception;
                        await SafeAuditAsync($"CSV writer failure: {exception.Message}", CancellationToken.None);

                        foreach (CompletedStay stay in batch)
                        {
                            Interlocked.Increment(ref _pendingRows);
                            await _channel.Writer.WriteAsync(stay, CancellationToken.None);
                        }

                        await Task.Delay(1000, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Application cancellation stops normal production, but queued rows
                // must still be drained before final shutdown.
                await DrainAfterCancellationAsync();
            }
            catch (Exception exception)
            {
                _writerException = exception;
                await SafeAuditAsync($"CSV writer task failure: {exception.Message}", CancellationToken.None);
            }
        }

        private async Task DrainAfterCancellationAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(CancellationToken.None))
                {
                    List<CompletedStay> batch = new();

                    while (batch.Count < 50 && _channel.Reader.TryRead(out CompletedStay? stay))
                    {
                        batch.Add(stay);
                        Interlocked.Decrement(ref _pendingRows);
                    }

                    if (batch.Count == 0)
                    {
                        continue;
                    }

                    try
                    {
                        await _csvFileHandler.AppendAsync(batch, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        _writerException = exception;
                        await SafeAuditAsync($"CSV writer failure: {exception.Message}", CancellationToken.None);

                        foreach (CompletedStay stay in batch)
                        {
                            Interlocked.Increment(ref _pendingRows);
                            await _channel.Writer.WriteAsync(stay, CancellationToken.None);
                        }

                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception exception)
            {
                _writerException = exception;
            }
        }

        private async Task SafeAuditAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                await _auditLogger.LogAsync(message, cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Audit log error: {exception.Message}");
            }
        }

        private async Task FillBatchAsync(List<CompletedStay> batch, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);

            while (batch.Count < 50)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    return;
                }

                using CancellationTokenSource batchCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(token);

                Task<CompletedStay> readTask = _channel.Reader.ReadAsync(batchCancellationTokenSource.Token).AsTask();
                Task delayTask = Task.Delay(remaining, batchCancellationTokenSource.Token);

                Task completedTask = await Task.WhenAny(readTask, delayTask);

                if (completedTask == readTask)
                {
                    batchCancellationTokenSource.Cancel();

                    try
                    {
                        CompletedStay stay = await readTask;
                        batch.Add(stay);
                        Interlocked.Decrement(ref _pendingRows);
                    }
                    catch (ChannelClosedException)
                    {
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                batchCancellationTokenSource.Cancel();
                return;
            }
        }
    }
}
