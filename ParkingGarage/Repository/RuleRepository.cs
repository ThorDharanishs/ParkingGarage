using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Interface;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Repository
{
    public class RuleRepository : IRuleRepository
    {
        private readonly Dictionary<VehicleType, ParkingRule> _rules;

        public RuleRepository()
        {
            _rules = new Dictionary<VehicleType, ParkingRule>
            {
                [VehicleType.Motorcycle] = new ParkingRule(
                    VehicleType.Motorcycle,
                    new[] { 1, 2, 3 },
                    1,
                    20,
                    5),

                [VehicleType.Car] = new ParkingRule(
                    VehicleType.Car,
                    new[] { 1, 2 },
                    1,
                    40,
                    10),

                [VehicleType.Van] = new ParkingRule(
                    VehicleType.Van,
                    new[] { 1 },
                    1,
                    60,
                    15),

                [VehicleType.Bus] = new ParkingRule(
                    VehicleType.Bus,
                    new[] { 1 },
                    2,
                    100,
                    25)
            };
        }

        public ParkingRule GetRule(VehicleType vehicleType)
        {
            return _rules[vehicleType];
        }

        public bool TryGetRule(VehicleType vehicleType, out ParkingRule? rule)
        {
            return _rules.TryGetValue(vehicleType, out rule);
        }
    }
}
