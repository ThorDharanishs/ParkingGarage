using ParkingGarage.Controller;
using ParkingGarage.Core.Interface;
using ParkingGarage.FileHelper;
using ParkingGarage.Helper;
using ParkingGarage.Repository;
using ParkingGarage.Service;
using ParkingGarage.View;

namespace ParkingGarage
{
    public class Program
    {
        public static async Task<int> Main()
        {
            using CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                InputHelper inputHelper = new();

                await Console.Out.WriteLineAsync("========== PARKING GARAGE SETUP ==========");
                int level1Capacity = await ReadCapacityAsync(inputHelper, 1, 20, cancellationToken);
                int level2Capacity = await ReadCapacityAsync(inputHelper, 2, 15, cancellationToken);
                int level3Capacity = await ReadCapacityAsync(inputHelper, 3, 10, cancellationToken);
                double overstayHours = await ReadOverstayHoursAsync(inputHelper, 4, cancellationToken);

                string basePath = AppContext.BaseDirectory;
                string csvPath = Path.Combine(basePath, "parking_history.csv");
                string auditPath = Path.Combine(basePath, "parking_audit.log");

                AuditLogger auditLogger = new(auditPath);
                ParkingRepository parkingRepository = new(
                    new[] { level1Capacity, level2Capacity, level3Capacity });
                HistoryRepository historyRepository = new();
                RuleRepository ruleRepository = new();
                GateService gateService = new(auditLogger);
                FeeService feeService = new();
                ParkingService parkingService = new(
                    parkingRepository,
                    ruleRepository,
                    gateService,
                    feeService);
                HistoryWriterService historyWriterService = new(csvPath, auditLogger);
                MonitoringService monitoringService = new(
                    parkingService,
                    auditLogger,
                    TimeSpan.FromHours(overstayHours));
                ReportService reportService = new(
                    historyRepository,
                    parkingService,
                    monitoringService,
                    historyWriterService);
                ConsoleActivity consoleActivity = new();
                ParkingController parkingController = new(
                    parkingService,
                    historyRepository,
                    historyWriterService,
                    monitoringService,
                    reportService,
                    gateService,
                    inputHelper,
                    consoleActivity,
                    auditLogger);

                historyWriterService.Start(cancellationToken);
                monitoringService.Start(cancellationToken);

                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync("Parking garage started successfully.");
                await Console.Out.WriteLineAsync($"History file: {csvPath}");
                await Console.Out.WriteLineAsync($"Overstay threshold: {overstayHours:0.##} hour(s)");

                await parkingController.StartAsync(cancellationToken);

                await ShutdownAsync(
                    cancellationTokenSource,
                    gateService,
                    historyWriterService,
                    monitoringService);

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Application error: {exception.Message}");
                return 1;
            }
        }

        private static async Task<int> ReadCapacityAsync(
            InputHelper inputHelper,
            int level,
            int defaultValue,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Console.Out.WriteAsync($"Level {level} capacity [{defaultValue}]: ");
                string? input = await inputHelper.ReadLineAsync(cancellationToken);

                if (input == null)
                {
                    return defaultValue;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (Validator.TryParsePositiveInteger(input, out int value))
                {
                    return value;
                }

                await Console.Out.WriteLineAsync("Enter a positive number or press Enter for the default.");
            }

            return defaultValue;
        }

        private static async Task<double> ReadOverstayHoursAsync(
            InputHelper inputHelper,
            double defaultValue,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Console.Out.WriteAsync($"Overstay threshold in hours [{defaultValue}]: ");
                string? input = await inputHelper.ReadLineAsync(cancellationToken);

                if (input == null || string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (double.TryParse(input, out double value) && value > 0)
                {
                    return value;
                }

                await Console.Out.WriteLineAsync("Enter a positive number or press Enter for the default.");
            }

            return defaultValue;
        }

        private static async Task ShutdownAsync(
            CancellationTokenSource cancellationTokenSource,
            GateService gateService,
            HistoryWriterService historyWriterService,
            MonitoringService monitoringService)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== SHUTTING DOWN ==========");

            // Menu exit reaches here with the token still active, so gate cycles can finish naturally.
            // Ctrl+C already cancelled the application token; gate cycles then stop cooperatively.
            await gateService.WaitForInFlightGatesAsync(TimeSpan.FromSeconds(10));

            // Complete the channel only after gate activity has stopped being created.
            // The writer is allowed to drain all queued rows before the application token is cancelled.
            historyWriterService.Complete();
            await historyWriterService.CompleteAndDrainAsync();

            cancellationTokenSource.Cancel();
            await monitoringService.WaitAsync();

            await Console.Out.WriteLineAsync("CSV writer drained.");
            await Console.Out.WriteLineAsync("Shutdown complete.");
            await Console.Out.WriteLineAsync("===================================");
        }
    }
}
