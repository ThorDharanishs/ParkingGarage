namespace ParkingGarage.Core.Model
{
    public class PeakOccupancy
    {
        public int Level { get; init; }
        public int Occupancy { get; init; }
        public DateTime ReachedAt { get; init; }
    }
}
