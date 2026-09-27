using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Model;
using ParkingGarage.Core.Interface;
using ParkingGarage.FileHelper;
using ParkingGarage.Helper;
using ParkingGarage.Service;
using ParkingGarage.View;

namespace ParkingGarage.Controller
{
    public class ParkingController
    {
        private readonly ParkingService _parkingService;
        private readonly IHistoryRepository _historyRepository;
        private readonly HistoryWriterService _historyWriterService;
        private readonly MonitoringService _monitoringService;
        private readonly ReportService _reportService;
        private readonly GateService _gateService;
        private readonly InputHelper _inputHelper;
        private readonly ConsoleActivity _consoleActivity;
        private readonly AuditLogger _auditLogger;

        public ParkingController(
            ParkingService parkingService,
            IHistoryRepository historyRepository,
            HistoryWriterService historyWriterService,
            MonitoringService monitoringService,
            ReportService reportService,
            GateService gateService,
            InputHelper inputHelper,
            ConsoleActivity consoleActivity,
            AuditLogger auditLogger)
        {
            _parkingService = parkingService;
            _historyRepository = historyRepository;
            _historyWriterService = historyWriterService;
            _monitoringService = monitoringService;
            _reportService = reportService;
            _gateService = gateService;
            _inputHelper = inputHelper;
            _consoleActivity = consoleActivity;
            _auditLogger = auditLogger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _consoleActivity.ShowMainMenuAsync(cancellationToken);
                    string? input = await _inputHelper.ReadLineAsync(cancellationToken);

                    if (input == null)
                    {
                        break;
                    }

                    if (!int.TryParse(input, out int option) || option < 1 || option > 8)
                    {
                        await Console.Out.WriteLineAsync("Invalid option. Please select 1 to 8.");
                        continue;
                    }

                    switch ((MenuOption)option)
                    {
                        case MenuOption.CheckIn:
                            await CheckInAsync(cancellationToken);
                            break;

                        case MenuOption.CheckOut:
                            await CheckOutAsync(cancellationToken);
                            break;

                        case MenuOption.LiveOccupancy:
                            await ShowLiveOccupancyAsync(cancellationToken);
                            break;

                        case MenuOption.FindVehicle:
                            await FindVehicleAsync(cancellationToken);
                            break;

                        case MenuOption.ListParkedVehicles:
                            await ListParkedVehiclesAsync(cancellationToken);
                            break;

                        case MenuOption.ShowOverstay:
                            await ShowOverstayAsync(cancellationToken);
                            break;

                        case MenuOption.EndOfDayReport:
                            await _reportService.PrintEndOfDayReportAsync(cancellationToken);
                            break;

                        case MenuOption.Exit:
                            return;
                    }

                    await ShowWriterWarningAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    await Console.Out.WriteLineAsync($"Action failed: {exception.Message}");
                    await SafeAuditAsync($"Controller action failure: {exception}", cancellationToken);
                }
            }
        }

        private async Task CheckInAsync(CancellationToken cancellationToken)
        {
            string? plate = await _inputHelper.ReadRequiredAsync(
                "Enter license plate: ",
                cancellationToken);

            if (plate == null)
            {
                return;
            }

            await Console.Out.WriteLineAsync("Vehicle type:");
            await Console.Out.WriteLineAsync("1. Motorcycle");
            await Console.Out.WriteLineAsync("2. Car");
            await Console.Out.WriteLineAsync("3. Van");
            await Console.Out.WriteAsync("Select vehicle type: ");

            string? typeInput = await _inputHelper.ReadLineAsync(cancellationToken);

            if (!Validator.TryParseVehicleType(typeInput, out VehicleType vehicleType) ||
                vehicleType == VehicleType.Bus)
            {
                await Console.Out.WriteLineAsync("Invalid vehicle type.");
                return;
            }

            if (!Validator.IsValidLicensePlate(plate))
            {
                await Console.Out.WriteLineAsync("Invalid license plate format.");
                return;
            }

            (bool success, Ticket? ticket, string message) =
                await _parkingService.CheckInAsync(plate, vehicleType, cancellationToken);

            await Console.Out.WriteLineAsync(message);

            if (success && ticket != null)
            {
                await _consoleActivity.ShowTicketAsync(ticket, cancellationToken);
                await Console.Out.WriteLineAsync("Entry gate cycle is running in the background.");
            }
        }

        private async Task CheckOutAsync(CancellationToken cancellationToken)
        {
            string? input = await _inputHelper.ReadRequiredAsync(
                "Enter ticket ID or license plate: ",
                cancellationToken);

            if (input == null)
            {
                return;
            }

            (bool success, CompletedStay? completedStay, Ticket? ticket, string message) =
                await _parkingService.CheckOutAsync(input, cancellationToken);

            await Console.Out.WriteLineAsync(message);

            if (!success || completedStay == null || ticket == null)
            {
                return;
            }

            await _consoleActivity.ShowReceiptAsync(completedStay, cancellationToken);
            await _historyRepository.AddCompletedStayAsync(completedStay, cancellationToken);
            await _historyWriterService.QueueAsync(completedStay, cancellationToken);
            await Console.Out.WriteLineAsync("Completed stay queued for asynchronous CSV persistence.");
            await Console.Out.WriteLineAsync("Exit gate cycle is running in the background.");
        }

        private async Task ShowLiveOccupancyAsync(CancellationToken cancellationToken)
        {
            await _consoleActivity.ShowLiveOccupancyAsync(
                _parkingService.GetSnapshotsAsync,
                _parkingService.GetOpenGateCycleCount,
                () => _historyWriterService.PendingRows,
                cancellationToken);
        }

        private async Task FindVehicleAsync(CancellationToken cancellationToken)
        {
            string? input = await _inputHelper.ReadRequiredAsync(
                "Enter ticket ID or license plate: ",
                cancellationToken);

            if (input == null)
            {
                return;
            }

            Ticket? ticket = await _parkingService.FindVehicleAsync(input, cancellationToken);

            if (ticket == null)
            {
                await Console.Out.WriteLineAsync("Vehicle not found.");
                return;
            }

            await _consoleActivity.ShowVehicleAsync(ticket, cancellationToken);
        }

        private async Task ListParkedVehiclesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Ticket> tickets = await _parkingService.GetOpenTicketsAsync(cancellationToken);
            await _consoleActivity.ShowParkedVehiclesAsync(tickets, cancellationToken);
        }

        private async Task ShowOverstayAsync(CancellationToken cancellationToken)
        {
            await _consoleActivity.ShowOverstaysAsync(
                _monitoringService.GetOverstays(),
                cancellationToken);
        }

        private async Task ShowWriterWarningAsync(CancellationToken cancellationToken)
        {
            if (!_historyWriterService.HasFaulted)
            {
                return;
            }

            await Console.Out.WriteLineAsync(
                $"WARNING: CSV writer previously failed. Pending rows are being retained. " +
                $"Error: {_historyWriterService.FaultMessage}");
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
                await Console.Out.WriteLineAsync($"Audit log error: {exception.Message}");
            }
        }
    }
}
