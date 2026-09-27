using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class Ticket
    {
        public Ticket(string ticketNumber, Vehicle vehicle, int levelNumber, DateTime entryTime)
        {
            TicketGuid = Guid.NewGuid();
            TicketNumber = ticketNumber;
            Vehicle = vehicle;
            LevelNumber = levelNumber;
            EntryTime = entryTime;
            SpotNumbers = Array.Empty<int>();
        }

        public Guid TicketGuid { get; init; }
        public string TicketNumber { get; }
        public Vehicle Vehicle { get; }
        public string LicensePlate => Vehicle.LicensePlate;
        public VehicleType VehicleType => Vehicle.VehicleType;
        public int LevelNumber { get; }
        public IReadOnlyList<int> SpotNumbers { get; internal set; }
        public int SpotNumber => SpotNumbers.Count == 0 ? 0 : SpotNumbers[0];
        public DateTime EntryTime { get; }
        public bool OverstayFlagged { get; set; }
    }
}
