using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class ParkingRule
    {
        public ParkingRule(
            VehicleType vehicleType,
            IEnumerable<int> allowedLevels,
            int requiredSpots,
            decimal firstHourFee,
            decimal additionalBlockFee)
        {
            VehicleType = vehicleType;
            AllowedLevels = allowedLevels.ToHashSet();
            RequiredSpots = requiredSpots;
            FirstHourFee = firstHourFee;
            AdditionalBlockFee = additionalBlockFee;
        }

        public VehicleType VehicleType { get; }
        public HashSet<int> AllowedLevels { get; }
        public int RequiredSpots { get; }
        public decimal FirstHourFee { get; }
        public decimal AdditionalBlockFee { get; }
    }
}
