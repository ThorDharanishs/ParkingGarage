namespace ParkingGarage.Core.Model
{
    public class OverstayRecord
    {
        public string TicketId { get; init; } = string.Empty;
        public string Plate { get; init; } = string.Empty;
        public DateTime EntryTime { get; init; }
        public DateTime FlaggedAt { get; init; }
        public TimeSpan Duration { get; init; }
        public int Level { get; init; }
        public int Spot { get; init; }
    }
}
