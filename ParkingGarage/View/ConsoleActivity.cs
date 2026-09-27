using ParkingGarage.Core.Constant;
using ParkingGarage.Core.Model;

namespace ParkingGarage.View
{
    public class ConsoleActivity
    {
        public async Task ShowMainMenuAsync(CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== PARKING GARAGE ==========");
            await Console.Out.WriteLineAsync("1. Check in vehicle");
            await Console.Out.WriteLineAsync("2. Check out vehicle");
            await Console.Out.WriteLineAsync("3. Show live occupancy");
            await Console.Out.WriteLineAsync("4. Find vehicle");
            await Console.Out.WriteLineAsync("5. List currently parked vehicles");
            await Console.Out.WriteLineAsync("6. Show overstay vehicles");
            await Console.Out.WriteLineAsync("7. End-of-day report");
            await Console.Out.WriteLineAsync("8. Exit");
            await Console.Out.WriteLineAsync("====================================");
            await Console.Out.WriteAsync("Select option: ");
        }

        public async Task ShowTicketAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== PARKING TICKET ==========");
            await Console.Out.WriteLineAsync($"Ticket ID    : {ticket.TicketNumber}");
            await Console.Out.WriteLineAsync($"Plate        : {ticket.LicensePlate}");
            await Console.Out.WriteLineAsync($"Vehicle      : {ticket.VehicleType}");
            await Console.Out.WriteLineAsync($"Level        : {ticket.LevelNumber}");
            await Console.Out.WriteLineAsync($"Spot         : {string.Join(", ", ticket.SpotNumbers)}");
            await Console.Out.WriteLineAsync($"Entry Time   : {ticket.EntryTime:yyyy-MM-dd HH:mm:ss}");
            await Console.Out.WriteLineAsync("===================================");
        }

        public async Task ShowReceiptAsync(CompletedStay stay, CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== PARKING RECEIPT ==========");
            await Console.Out.WriteLineAsync($"Ticket ID    : {stay.TicketId}");
            await Console.Out.WriteLineAsync($"Plate        : {stay.Plate}");
            await Console.Out.WriteLineAsync($"Vehicle      : {stay.Type}");
            await Console.Out.WriteLineAsync($"Level / Spot : {stay.Level} / {stay.Spot}");
            await Console.Out.WriteLineAsync($"Entry        : {stay.EntryTime:yyyy-MM-dd HH:mm:ss}");
            await Console.Out.WriteLineAsync($"Exit         : {stay.ExitTime:yyyy-MM-dd HH:mm:ss}");
            await Console.Out.WriteLineAsync($"Duration     : {stay.DurationMinutes} minutes");
            await Console.Out.WriteLineAsync($"Fee          : {stay.Fee:0.00}");
            await Console.Out.WriteLineAsync("=====================================");
        }

        public async Task ShowLiveOccupancyAsync(
            Func<CancellationToken, Task<IReadOnlyList<ParkingLevelSnapshot>>> snapshotProvider,
            Func<int> openGateCountProvider,
            Func<int> pendingRowsProvider,
            CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync("Live occupancy started. Press any key to return.");
            await RenderLiveOccupancyAsync(snapshotProvider, openGateCountProvider, pendingRowsProvider, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(1));

            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                    break;
                }

                try
                {
                    bool ticked = await timer.WaitForNextTickAsync(cancellationToken);

                    if (!ticked)
                    {
                        break;
                    }

                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey(true);
                        break;
                    }

                    await RenderLiveOccupancyAsync(
                        snapshotProvider,
                        openGateCountProvider,
                        pendingRowsProvider,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static async Task RenderLiveOccupancyAsync(
            Func<CancellationToken, Task<IReadOnlyList<ParkingLevelSnapshot>>> snapshotProvider,
            Func<int> openGateCountProvider,
            Func<int> pendingRowsProvider,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ParkingLevelSnapshot> snapshots = await snapshotProvider(cancellationToken);

            Console.Clear();
            await Console.Out.WriteLineAsync("========== LIVE OCCUPANCY ==========");

            int totalOccupied = 0;
            int totalCapacity = 0;

            foreach (ParkingLevelSnapshot snapshot in snapshots)
            {
                totalOccupied += snapshot.OccupiedCount;
                totalCapacity += snapshot.Capacity;

                string spots = string.Concat(snapshot.SpotCodes.Select(code => $"[{code}]"));

                await Console.Out.WriteLineAsync(
                    $"Level {snapshot.LevelNumber} ({snapshot.OccupiedCount}/{snapshot.Capacity}) " +
                    $"Entry: {snapshot.EntryGateState,-7} Exit: {snapshot.ExitGateState,-7} {spots}");
            }

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync(
                $"Totals: {totalOccupied}/{totalCapacity} " +
                $"Open gate cycles: {openGateCountProvider()} " +
                $"Pending CSV rows: {pendingRowsProvider()}");
            await Console.Out.WriteLineAsync("Press any key to return.");
        }

        public async Task ShowParkedVehiclesAsync(
            IReadOnlyList<Ticket> tickets,
            CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== CURRENTLY PARKED ==========");

            if (tickets.Count == 0)
            {
                await Console.Out.WriteLineAsync("No vehicles are currently parked.");
                return;
            }

            foreach (Ticket ticket in tickets.OrderBy(item => item.LevelNumber).ThenBy(item => item.SpotNumber))
            {
                await Console.Out.WriteLineAsync(
                    $"{ticket.TicketNumber} | {ticket.LicensePlate} | {ticket.VehicleType} | " +
                    $"Level {ticket.LevelNumber} | Spot {string.Join(",", ticket.SpotNumbers)} | " +
                    $"Entry {ticket.EntryTime:HH:mm:ss}");
            }
        }

        public async Task ShowOverstaysAsync(
            IReadOnlyList<OverstayRecord> overstays,
            CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== OVERSTAY VEHICLES ==========");

            if (overstays.Count == 0)
            {
                await Console.Out.WriteLineAsync("No overstays have been flagged.");
                return;
            }

            foreach (OverstayRecord record in overstays)
            {
                await Console.Out.WriteLineAsync(
                    $"{record.TicketId} | {record.Plate} | Level {record.Level} | Spot {record.Spot} | " +
                    $"Duration {record.Duration.TotalMinutes:0} min | Flagged {record.FlaggedAt:HH:mm:ss}");
            }
        }

        public async Task ShowVehicleAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("========== VEHICLE ==========");
            await Console.Out.WriteLineAsync($"Ticket ID : {ticket.TicketNumber}");
            await Console.Out.WriteLineAsync($"Plate     : {ticket.LicensePlate}");
            await Console.Out.WriteLineAsync($"Type      : {ticket.VehicleType}");
            await Console.Out.WriteLineAsync($"Level     : {ticket.LevelNumber}");
            await Console.Out.WriteLineAsync($"Spot      : {string.Join(", ", ticket.SpotNumbers)}");
            await Console.Out.WriteLineAsync($"Entry     : {ticket.EntryTime:yyyy-MM-dd HH:mm:ss}");
        }
    }
}
