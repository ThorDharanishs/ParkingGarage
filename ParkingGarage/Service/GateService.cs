using System.Collections.Concurrent;
using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;
using ParkingGarage.FileHelper;

namespace ParkingGarage.Service
{
    public class GateService : IGateService
    {
        private readonly ConcurrentDictionary<Guid, Task> _inFlightGateTasks = new();
        private readonly AuditLogger _auditLogger;

        public GateService(AuditLogger auditLogger)
        {
            _auditLogger = auditLogger;
        }

        public Task<bool> TryStartEntryGateAsync(ParkingLevel level, CancellationToken cancellationToken)
        {
            return TryStartGateAsync(level.EntryGate, level.LevelNumber, "Entry", cancellationToken);
        }

        public Task<bool> TryStartExitGateAsync(ParkingLevel level, CancellationToken cancellationToken)
        {
            return TryStartGateAsync(level.ExitGate, level.LevelNumber, "Exit", cancellationToken);
        }

        public int GetOpenGateCycleCount()
        {
            return _inFlightGateTasks.Count;
        }

        public IReadOnlyList<Task> GetInFlightTasks()
        {
            return _inFlightGateTasks.Values.ToList();
        }

        public async Task WaitForInFlightGatesAsync(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (_inFlightGateTasks.Count > 0)
            {
                Task[] tasks = _inFlightGateTasks.Values.ToArray();

                if (tasks.Length == 0)
                {
                    return;
                }

                TimeSpan remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    return;
                }

                Task allTasks = Task.WhenAll(tasks);
                Task delayTask = Task.Delay(remaining);
                Task completedTask = await Task.WhenAny(allTasks, delayTask);

                if (completedTask == delayTask)
                {
                    return;
                }
            }
        }

        private async Task<bool> TryStartGateAsync(
            Gate gate,
            int levelNumber,
            string gateType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool acquired = await gate.CycleSemaphore.WaitAsync(0, cancellationToken);

            if (!acquired)
            {
                return false;
            }

            Task gateTask = RunGateCycleAsync(gate, levelNumber, gateType, cancellationToken);
            Guid taskId = Guid.NewGuid();
            _inFlightGateTasks[taskId] = gateTask;

            _ = RemoveCompletedTaskAsync(taskId, gateTask);
            return true;
        }

        private async Task RemoveCompletedTaskAsync(Guid taskId, Task gateTask)
        {
            try
            {
                await gateTask;
            }
            finally
            {
                _inFlightGateTasks.TryRemove(taskId, out _);
            }
        }

        private async Task RunGateCycleAsync(
            Gate gate,
            int levelNumber,
            string gateType,
            CancellationToken cancellationToken)
        {
            try
            {
                gate.SetState(GateState.Opening);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                gate.SetState(GateState.Open);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                gate.SetState(GateState.Closing);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                gate.SetState(GateState.Idle);
            }
            catch (OperationCanceledException)
            {
                gate.SetState(GateState.Idle);
            }
            catch (Exception exception)
            {
                gate.SetState(GateState.Idle);
                await SafeAuditAsync(
                    $"{gateType} gate failure on Level {levelNumber}: {exception.Message}",
                    cancellationToken);
            }
            finally
            {
                gate.SetState(GateState.Idle);
                gate.CycleSemaphore.Release();
            }
        }

        private async Task SafeAuditAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                await _auditLogger.LogAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Audit log error: {exception.Message}");
            }
        }
    }
}
