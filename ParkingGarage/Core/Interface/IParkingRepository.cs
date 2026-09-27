using ParkingGarage.Core.Model;

namespace ParkingGarage.Core.Interface
{
    public interface IParkingRepository
    {
        Task<IReadOnlyList<ParkingLevel>> GetLevelsAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<Ticket>> GetOpenTicketsAsync(CancellationToken cancellationToken);
        Task<Ticket?> FindTicketAsync(string input, CancellationToken cancellationToken);
        Task<bool> ContainsPlateAsync(string plate, CancellationToken cancellationToken);
        Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken);
        Task<bool> RemoveTicketAsync(Ticket ticket, CancellationToken cancellationToken);
        Task<bool> TryReserveSpotsAsync(ParkingLevel level, int requiredSpots, Ticket ticket, CancellationToken cancellationToken);
        Task ReleaseSpotsAsync(Ticket ticket, CancellationToken cancellationToken);
        Task<ParkingLevel?> GetLevelAsync(int levelNumber, CancellationToken cancellationToken);
        Task<IReadOnlyList<ParkingLevelSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken);
        IReadOnlyList<PeakOccupancy> GetPeakOccupancy();
    }
}
