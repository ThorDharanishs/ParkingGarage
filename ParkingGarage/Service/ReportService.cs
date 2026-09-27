using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Service
{
    public class ReportService
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly ParkingService _parkingService;
        private readonly MonitoringService _monitoringService;
        private readonly HistoryWriterService _historyWriterService;

        public ReportService(
            IHistoryRepository historyRepository,
            ParkingService parkingService,
            MonitoringService monitoringService,
            HistoryWriterService historyWriterService)
        {
            _historyRepository = historyRepository;
            _parkingService = parkingService;
            _monitoringService = monitoringService;
            _historyWriterService = historyWriterService;
        }

        public async Task PrintEndOfDayReportAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<CompletedStay> stays =
                await _historyRepository.GetCompletedStaysAsync(cancellationToken);

            IReadOnlyList<PeakOccupancy> peaks = _parkingService.GetPeakOccupancy();
            IReadOnlyList<OverstayRecord> overstays = _monitoringService.GetOverstays();

            Console.WriteLine();
            Console.WriteLine("========== END OF DAY REPORT ==========");
            Console.WriteLine($"Total vehicles served today : {stays.Count}");
            Console.WriteLine();
            Console.WriteLine("Revenue by vehicle type:");

            foreach (VehicleType type in Enum.GetValues<VehicleType>())
            {
                decimal revenue = stays
                    .Where(stay => stay.Type == type)
                    .Sum(stay => stay.Fee);

                int count = stays.Count(stay => stay.Type == type);
                Console.WriteLine($"{type,-12}: {count,4} vehicles | {revenue,8:0.00}");
            }

            Console.WriteLine($"Total revenue                : {stays.Sum(stay => stay.Fee):0.00}");
            Console.WriteLine();
            Console.WriteLine("Peak occupancy:");

            foreach (PeakOccupancy peak in peaks)
            {
                Console.WriteLine(
                    $"Level {peak.Level}: {peak.Occupancy} spots at {peak.ReachedAt:yyyy-MM-dd HH:mm:ss}");
            }

            Console.WriteLine();
            Console.WriteLine("Average parking duration:");

            foreach (VehicleType type in Enum.GetValues<VehicleType>())
            {
                List<CompletedStay> typeStays = stays
                    .Where(stay => stay.Type == type)
                    .ToList();

                double average = typeStays.Count == 0
                    ? 0
                    : typeStays.Average(stay => stay.DurationMinutes);

                Console.WriteLine($"{type,-12}: {average:0.00} minutes");
            }

            Console.WriteLine();
            Console.WriteLine($"Overstays flagged             : {overstays.Count}");
            Console.WriteLine($"Pending CSV rows              : {_historyWriterService.PendingRows}");
            Console.WriteLine("=======================================");
            Console.WriteLine();
        }
    }
}
