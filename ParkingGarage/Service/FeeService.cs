using ParkingGarage.Core.Model;

namespace ParkingGarage.Service
{
    public class FeeService
    {
        public decimal CalculateFee(Ticket ticket, DateTime exitTime, ParkingRule rule)
        {
            TimeSpan duration = exitTime - ticket.EntryTime;
            long startedBlocks = Math.Max(1, (long)Math.Ceiling(duration.TotalMinutes / 15.0));

            decimal fee = rule.FirstHourFee;

            if (startedBlocks > 4)
            {
                fee += (startedBlocks - 4) * rule.AdditionalBlockFee;
            }

            decimal dailyCap = rule.FirstHourFee + (32 - 4) * rule.AdditionalBlockFee;
            return Math.Min(fee, dailyCap);
        }
    }
}
