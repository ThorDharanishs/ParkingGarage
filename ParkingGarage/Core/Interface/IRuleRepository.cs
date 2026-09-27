using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Model;

namespace ParkingGarage.Core.Interface
{
    public interface IRuleRepository
    {
        ParkingRule GetRule(VehicleType vehicleType);
        bool TryGetRule(VehicleType vehicleType, out ParkingRule? rule);
    }
}
