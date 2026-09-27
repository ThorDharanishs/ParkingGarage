using System.Collections.Concurrent;
using ParkingGarage.Core.Model;
using ParkingGarage.FileHelper;

namespace ParkingGarage.Service
{
    public class MonitoringService
    {
        private readonly ParkingService _parkingService;
        private readonly AuditLogger _auditLogger;
        private readonly TimeSpan _overstayThreshold;
        private readonly ConcurrentDictionary<Guid, OverstayRecord> _overstays = new();
        private Task? _monitorTask;

        public MonitoringService(
            ParkingService parkingService,
            AuditLogger auditLogger,
            TimeSpan overstayThreshold)
        {
            _parkingService = parkingService;
            _auditLogger = auditLogger;
            _overstayThreshold = overstayThreshold;
        }

        public void Start(CancellationToken cancellationToken)
        {
            _monitorTask = MonitorAsync(cancellationToken);
        }

        public Task WaitAsync()
        {
            return _monitorTask ?? Task.CompletedTask;
        }

        public IReadOnlyList<OverstayRecord> GetOverstays()
        {
            return _overstays.Values
                .OrderBy(item => item.FlaggedAt)
                .ToList();
        }

        private async Task MonitorAsync(CancellationToken cancellationToken)
        {
            try
            {
                using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    IReadOnlyList<Ticket> tickets =
                        await _parkingService.GetOpenTicketsAsync(cancellationToken);

                    DateTime now = DateTime.Now;

                    foreach (Ticket ticket in tickets)
                    {
                        if (ticket.OverstayFlagged || now - ticket.EntryTime < _overstayThreshold)
                        {
                            continue;
                        }

                        OverstayRecord record = new()
                        {
                            TicketId = ticket.TicketNumber,
                            Plate = ticket.LicensePlate,
                            EntryTime = ticket.EntryTime,
                            FlaggedAt = now,
                            Duration = now - ticket.EntryTime,
                            Level = ticket.LevelNumber,
                            Spot = ticket.SpotNumber
                        };

                        if (_overstays.TryAdd(ticket.TicketGuid, record))
                        {
                            ticket.OverstayFlagged = true;
                            await SafeAuditAsync(
                                $"Overstay flagged: {ticket.TicketNumber}, {ticket.LicensePlate}, Level {ticket.LevelNumber}, Spot {ticket.SpotNumber}",
                                cancellationToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cooperative shutdown.
            }
            catch (Exception exception)
            {
                await SafeAuditAsync($"Overstay monitor failure: {exception.Message}", CancellationToken.None);
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
