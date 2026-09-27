using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class ParkingSpot
    {
        public ParkingSpot(int spotNumber)
        {
            SpotNumber = spotNumber;
            State = ParkingSpotState.Available;
        }

        public int SpotNumber { get; }
        public ParkingSpotState State { get; set; }
        public string? VehicleTypeCode { get; set; }
        public Guid? TicketId { get; set; }

        public void Occupy(Ticket ticket)
        {
            State = ParkingSpotState.Occupied;
            VehicleTypeCode = ticket.VehicleType.ToString();
            TicketId = ticket.TicketGuid;
        }

        public void Release()
        {
            State = ParkingSpotState.Available;
            VehicleTypeCode = null;
            TicketId = null;
        }
    }
}
