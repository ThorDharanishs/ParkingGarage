using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class CompletedStay
    {
        public string TicketId { get; init; } = string.Empty;
        public string Plate { get; init; } = string.Empty;
        public VehicleType Type { get; init; }
        public int Level { get; init; }
        public string Spot { get; init; } = string.Empty;
        public DateTime EntryTime { get; init; }
        public DateTime ExitTime { get; init; }
        public long DurationMinutes { get; init; }
        public decimal Fee { get; init; }
    }
}
