namespace ParkingGarage.Core.Constant
{
    public enum VehicleType
    {
        Motorcycle = 1,
        Car,
        Van,
        Bus
    }

    public enum GateState
    {
        Idle = 1,
        Opening,
        Open,
        Closing
    }

    public enum ParkingSpotState
    {
        Available = 1,
        Occupied
    }

    public enum MenuOption
    {
        CheckIn = 1,
        CheckOut,
        LiveOccupancy,
        FindVehicle,
        ListParkedVehicles,
        ShowOverstay,
        EndOfDayReport,
        Exit
    }
}
