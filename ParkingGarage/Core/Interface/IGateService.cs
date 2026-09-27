using ParkingGarage.Core.Model;

namespace ParkingGarage.Core.Interface
{
    public interface IGateService
    {
        Task<bool> TryStartEntryGateAsync(ParkingLevel level, CancellationToken cancellationToken);
        Task<bool> TryStartExitGateAsync(ParkingLevel level, CancellationToken cancellationToken);
        int GetOpenGateCycleCount();
        IReadOnlyList<Task> GetInFlightTasks();
        Task WaitForInFlightGatesAsync(TimeSpan timeout);
    }
}
