using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Service
{
    public class ParkingService
    {
        private readonly IParkingRepository _parkingRepository;
        private readonly IRuleRepository _ruleRepository;
        private readonly IGateService _gateService;
        private readonly FeeService _feeService;
        private int _ticketNumber;

        public ParkingService(
            IParkingRepository parkingRepository,
            IRuleRepository ruleRepository,
            IGateService gateService,
            FeeService feeService)
        {
            _parkingRepository = parkingRepository;
            _ruleRepository = ruleRepository;
            _gateService = gateService;
            _feeService = feeService;
        }

        public async Task<(bool Success, Ticket? Ticket, string Message)> CheckInAsync(
            string licensePlate,
            VehicleType vehicleType,
            CancellationToken cancellationToken)
        {
            string normalizedPlate = licensePlate.Trim().ToUpperInvariant();

            if (await _parkingRepository.ContainsPlateAsync(normalizedPlate, cancellationToken))
            {
                return (false, null, "This license plate is already parked.");
            }

            if (!_ruleRepository.TryGetRule(vehicleType, out ParkingRule? rule) || rule == null)
            {
                return (false, null, "No parking rule is configured for this vehicle type.");
            }

            IReadOnlyList<ParkingLevel> levels = await _parkingRepository.GetLevelsAsync(cancellationToken);
            ParkingLevel? selectedLevel = null;

            foreach (int levelNumber in rule.AllowedLevels.OrderBy(level => level))
            {
                ParkingLevel? level = levels.FirstOrDefault(item => item.LevelNumber == levelNumber);

                if (level != null && HasRequiredContiguousSpace(level, rule.RequiredSpots))
                {
                    selectedLevel = level;
                    break;
                }
            }

            if (selectedLevel == null)
            {
                bool spaceExistsOnDisallowedLevel = levels.Any(level =>
                    !rule.AllowedLevels.Contains(level.LevelNumber) &&
                    HasRequiredContiguousSpace(level, rule.RequiredSpots));

                if (spaceExistsOnDisallowedLevel)
                {
                    return (false, null,
                        $"{vehicleType} cannot park because the available space is on a level where this vehicle type is not allowed.");
                }

                return (false, null, $"Garage is full for {vehicleType}.");
            }

            Vehicle vehicle = new(normalizedPlate, vehicleType);
            string ticketNumber = $"T-{Interlocked.Increment(ref _ticketNumber):D4}";
            Ticket ticket = new(ticketNumber, vehicle, selectedLevel.LevelNumber, DateTime.Now);

            bool reserved = await _parkingRepository.TryReserveSpotsAsync(
                selectedLevel,
                rule.RequiredSpots,
                ticket,
                cancellationToken);

            if (!reserved)
            {
                return (false, null, "The selected parking space became unavailable. Please try again.");
            }

            await _parkingRepository.AddTicketAsync(ticket, cancellationToken);

            bool gateStarted = await _gateService.TryStartEntryGateAsync(selectedLevel, cancellationToken);

            if (!gateStarted)
            {
                await _parkingRepository.RemoveTicketAsync(ticket, cancellationToken);
                await _parkingRepository.ReleaseSpotsAsync(ticket, cancellationToken);
                return (false, null, $"Level {selectedLevel.LevelNumber} entry gate busy — please wait.");
            }

            return (true, ticket, "Vehicle checked in successfully.");
        }

        public async Task<(bool Success, CompletedStay? CompletedStay, Ticket? Ticket, string Message)> CheckOutAsync(
            string input,
            CancellationToken cancellationToken)
        {
            Ticket? ticket = await _parkingRepository.FindTicketAsync(input, cancellationToken);

            if (ticket == null)
            {
                return (false, null, null, "No parked vehicle was found for that ticket ID or license plate.");
            }

            if (!_ruleRepository.TryGetRule(ticket.VehicleType, out ParkingRule? rule) || rule == null)
            {
                return (false, null, ticket, "Parking rule for this vehicle type was not found.");
            }

            DateTime exitTime = DateTime.Now;
            long durationMinutes = Math.Max(0, (long)Math.Ceiling((exitTime - ticket.EntryTime).TotalMinutes));
            decimal fee = _feeService.CalculateFee(ticket, exitTime, rule);

            bool removed = await _parkingRepository.RemoveTicketAsync(ticket, cancellationToken);

            if (!removed)
            {
                return (false, null, ticket, "The vehicle was already checked out.");
            }

            await _parkingRepository.ReleaseSpotsAsync(ticket, cancellationToken);

            CompletedStay completedStay = new()
            {
                TicketId = ticket.TicketNumber,
                Plate = ticket.LicensePlate,
                Type = ticket.VehicleType,
                Level = ticket.LevelNumber,
                Spot = string.Join("-", ticket.SpotNumbers),
                EntryTime = ticket.EntryTime,
                ExitTime = exitTime,
                DurationMinutes = durationMinutes,
                Fee = fee
            };

            ParkingLevel? level = await _parkingRepository.GetLevelAsync(ticket.LevelNumber, cancellationToken);

            if (level != null)
            {
                await _gateService.TryStartExitGateAsync(level, cancellationToken);
            }

            return (true, completedStay, ticket, "Vehicle checked out successfully.");
        }

        public Task<IReadOnlyList<Ticket>> GetOpenTicketsAsync(CancellationToken cancellationToken)
        {
            return _parkingRepository.GetOpenTicketsAsync(cancellationToken);
        }

        public Task<Ticket?> FindVehicleAsync(string input, CancellationToken cancellationToken)
        {
            return _parkingRepository.FindTicketAsync(input, cancellationToken);
        }

        public Task<IReadOnlyList<ParkingLevelSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken)
        {
            return _parkingRepository.GetSnapshotsAsync(cancellationToken);
        }

        public IReadOnlyList<PeakOccupancy> GetPeakOccupancy()
        {
            return _parkingRepository.GetPeakOccupancy();
        }

        public int GetOpenGateCycleCount()
        {
            return _gateService.GetOpenGateCycleCount();
        }

        private static bool HasRequiredContiguousSpace(ParkingLevel level, int requiredSpots)
        {
            int consecutive = 0;

            foreach (ParkingSpot spot in level.Spots.OrderBy(item => item.SpotNumber))
            {
                if (spot.State == ParkingSpotState.Available)
                {
                    consecutive++;

                    if (consecutive == requiredSpots)
                    {
                        return true;
                    }
                }
                else
                {
                    consecutive = 0;
                }
            }

            return false;
        }
    }
}
