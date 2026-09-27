using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class Vehicle
    {
        public Vehicle(string licensePlate, VehicleType vehicleType)
        {
            VehicleId = Guid.NewGuid();
            LicensePlate = licensePlate;
            VehicleType = vehicleType;
        }

        public Guid VehicleId { get; init; }
        public string LicensePlate { get; set; }
        public VehicleType VehicleType { get; set; }
    }
}
