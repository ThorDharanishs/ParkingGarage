using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class ParkingLevelSnapshot
    {
        public int LevelNumber { get; init; }
        public int Capacity { get; init; }
        public int OccupiedCount { get; init; }
        public IReadOnlyList<string> SpotCodes { get; init; } = Array.Empty<string>();
        public GateState EntryGateState { get; init; }
        public GateState ExitGateState { get; init; }
    }
}
