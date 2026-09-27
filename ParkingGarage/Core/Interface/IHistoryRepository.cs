using ParkingGarage.Core.Model;

namespace ParkingGarage.Core.Interface
{
    public interface IHistoryRepository
    {
        Task AddCompletedStayAsync(CompletedStay completedStay, CancellationToken cancellationToken);
        Task<IReadOnlyList<CompletedStay>> GetCompletedStaysAsync(CancellationToken cancellationToken);
        int Count { get; }
    }
}
