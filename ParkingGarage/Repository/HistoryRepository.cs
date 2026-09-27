using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Repository
{
    public class HistoryRepository : IHistoryRepository
    {
        private readonly List<CompletedStay> _completedStays = new();
        private readonly SemaphoreSlim _historySemaphore = new(1, 1);

        public int Count => _completedStays.Count;

        public async Task AddCompletedStayAsync(CompletedStay completedStay, CancellationToken cancellationToken)
        {
            await _historySemaphore.WaitAsync(cancellationToken);
            try
            {
                _completedStays.Add(completedStay);
            }
            finally
            {
                _historySemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<CompletedStay>> GetCompletedStaysAsync(CancellationToken cancellationToken)
        {
            await _historySemaphore.WaitAsync(cancellationToken);
            try
            {
                return _completedStays.ToList();
            }
            finally
            {
                _historySemaphore.Release();
            }
        }
    }
}
