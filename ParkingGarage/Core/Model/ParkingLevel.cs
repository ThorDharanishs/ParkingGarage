namespace ParkingGarage.Core.Model
{
    public class ParkingLevel
    {
        public ParkingLevel(int levelNumber, int capacity)
        {
            LevelNumber = levelNumber;
            Capacity = capacity;
            Spots = Enumerable.Range(1, capacity)
                .Select(spotNumber => new ParkingSpot(spotNumber))
                .ToList();

            EntryGate = new Gate($"Level {levelNumber} Entry");
            ExitGate = new Gate($"Level {levelNumber} Exit");
        }

        public int LevelNumber { get; }
        public int Capacity { get; }
        public List<ParkingSpot> Spots { get; }
        public Gate EntryGate { get; }
        public Gate ExitGate { get; }

        public int OccupiedCount => Spots.Count(spot => spot.State == Core.Constant.ParkingSpotState.Occupied);
    }
}
