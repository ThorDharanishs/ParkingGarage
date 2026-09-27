using System.Text.RegularExpressions;
using ParkingGarage.Core.Constant;

namespace ParkingGarage.Helper
{
    public class Validator
    {
        public static bool IsValidLicensePlate(string? plate)
        {
            if (string.IsNullOrWhiteSpace(plate))
            {
                return false;
            }

            return Regex.IsMatch(plate.Trim(), "^[A-Za-z0-9 -]{2,15}$");
        }

        public static bool TryParseVehicleType(string? input, out VehicleType vehicleType)
        {
            if (int.TryParse(input, out int number) &&
                Enum.IsDefined(typeof(VehicleType), number))
            {
                vehicleType = (VehicleType)number;
                return true;
            }

            if (Enum.TryParse(input, true, out vehicleType) &&
                Enum.IsDefined(typeof(VehicleType), vehicleType))
            {
                return true;
            }

            vehicleType = default;
            return false;
        }

        public static bool TryParsePositiveInteger(string? input, out int value)
        {
            return int.TryParse(input, out value) && value > 0;
        }
    }
}
