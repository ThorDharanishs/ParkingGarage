using System.Collections.Concurrent;
using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Repository
{
    public class ParkingRepository : IParkingRepository
    {
        private readonly List<ParkingLevel> _levels;
        private readonly ConcurrentDictionary<Guid, Ticket> _ticketsById = new();
        private readonly ConcurrentDictionary<string, Guid> _ticketIdsByPlate = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
        private readonly Dictionary<int, PeakOccupancy> _peakOccupancy = new();

        public ParkingRepository(IReadOnlyList<int> capacities)
        {
            if (capacities.Count != 3)
            {
                throw new ArgumentException("Exactly three parking levels are required.");
            }

            _levels = capacities
                .Select((capacity, index) => new ParkingLevel(index + 1, capacity))
                .ToList();

            foreach (ParkingLevel level in _levels)
            {
                _peakOccupancy[level.LevelNumber] = new PeakOccupancy
                {
                    Level = level.LevelNumber,
                    Occupancy = 0,
                    ReachedAt = DateTime.Now
                };
            }
        }

        public async Task<IReadOnlyList<ParkingLevel>> GetLevelsAsync(CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                return _levels.ToList();
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public Task<IReadOnlyList<Ticket>> GetOpenTicketsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Ticket>>(_ticketsById.Values.ToList());
        }

        public Task<Ticket?> FindTicketAsync(string input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalized = input.Trim().ToUpperInvariant();

            if (_ticketIdsByPlate.TryGetValue(normalized, out Guid ticketGuid) &&
                _ticketsById.TryGetValue(ticketGuid, out Ticket? plateTicket))
            {
                return Task.FromResult<Ticket?>(plateTicket);
            }

            Ticket? ticket = _ticketsById.Values.FirstOrDefault(item =>
                item.TicketNumber.Equals(normalized, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(ticket);
        }

        public Task<bool> ContainsPlateAsync(string plate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_ticketIdsByPlate.ContainsKey(plate));
        }

        public async Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                _ticketsById[ticket.TicketGuid] = ticket;
                _ticketIdsByPlate[ticket.LicensePlate] = ticket.TicketGuid;
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public async Task<bool> RemoveTicketAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                bool removed = _ticketsById.TryRemove(ticket.TicketGuid, out _);
                _ticketIdsByPlate.TryRemove(ticket.LicensePlate, out _);
                return removed;
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public async Task<bool> TryReserveSpotsAsync(
            ParkingLevel level,
            int requiredSpots,
            Ticket ticket,
            CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                List<ParkingSpot> availableSpots = FindFirstContiguousSpots(level, requiredSpots);

                if (availableSpots.Count != requiredSpots)
                {
                    return false;
                }

                ticket.SpotNumbers = availableSpots
                    .Select(spot => spot.SpotNumber)
                    .ToList();

                foreach (ParkingSpot spot in availableSpots)
                {
                    spot.Occupy(ticket);
                }

                UpdatePeakOccupancy(level);
                return true;
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public async Task ReleaseSpotsAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                ParkingLevel? level = _levels.FirstOrDefault(item => item.LevelNumber == ticket.LevelNumber);

                if (level == null)
                {
                    return;
                }

                foreach (int spotNumber in ticket.SpotNumbers)
                {
                    ParkingSpot? spot = level.Spots.FirstOrDefault(item => item.SpotNumber == spotNumber);
                    spot?.Release();
                }
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public async Task<ParkingLevel?> GetLevelAsync(int levelNumber, CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                return _levels.FirstOrDefault(item => item.LevelNumber == levelNumber);
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ParkingLevelSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken)
        {
            await _stateSemaphore.WaitAsync(cancellationToken);
            try
            {
                return _levels.Select(level => new ParkingLevelSnapshot
                {
                    LevelNumber = level.LevelNumber,
                    Capacity = level.Capacity,
                    OccupiedCount = level.OccupiedCount,
                    SpotCodes = level.Spots.Select(spot =>
                        spot.State == ParkingSpotState.Available
                            ? " "
                            : GetVehicleCode(spot.VehicleTypeCode)).ToList(),
                    EntryGateState = level.EntryGate.State,
                    ExitGateState = level.ExitGate.State
                }).ToList();
            }
            finally
            {
                _stateSemaphore.Release();
            }
        }

        public IReadOnlyList<PeakOccupancy> GetPeakOccupancy()
        {
            return _peakOccupancy.Values
                .Select(item => new PeakOccupancy
                {
                    Level = item.Level,
                    Occupancy = item.Occupancy,
                    ReachedAt = item.ReachedAt
                })
                .OrderBy(item => item.Level)
                .ToList();
        }

        private List<ParkingSpot> FindFirstContiguousSpots(ParkingLevel level, int requiredSpots)
        {
            List<ParkingSpot> selected = new();

            foreach (ParkingSpot spot in level.Spots.OrderBy(item => item.SpotNumber))
            {
                if (spot.State == ParkingSpotState.Available)
                {
                    if (selected.Count == 0 || spot.SpotNumber == selected[^1].SpotNumber + 1)
                    {
                        selected.Add(spot);
                    }
                    else
                    {
                        selected.Clear();
                        selected.Add(spot);
                    }

                    if (selected.Count == requiredSpots)
                    {
                        return selected;
                    }
                }
                else
                {
                    selected.Clear();
                }
            }

            return new List<ParkingSpot>();
        }

        private void UpdatePeakOccupancy(ParkingLevel level)
        {
            int currentOccupancy = level.OccupiedCount;
            PeakOccupancy currentPeak = _peakOccupancy[level.LevelNumber];

            if (currentOccupancy > currentPeak.Occupancy)
            {
                _peakOccupancy[level.LevelNumber] = new PeakOccupancy
                {
                    Level = level.LevelNumber,
                    Occupancy = currentOccupancy,
                    ReachedAt = DateTime.Now
                };
            }
        }

        private static string GetVehicleCode(string? vehicleType)
        {
            return vehicleType switch
            {
                nameof(VehicleType.Car) => "C",
                nameof(VehicleType.Motorcycle) => "M",
                nameof(VehicleType.Van) => "V",
                nameof(VehicleType.Bus) => "B",
                _ => "?"
            };
        }
    }
}
