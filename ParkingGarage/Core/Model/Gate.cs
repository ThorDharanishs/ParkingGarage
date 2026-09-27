using ParkingGarage.Core.Constant;

namespace ParkingGarage.Core.Model
{
    public class Gate
    {
        private int _state = (int)GateState.Idle;

        public Gate(string name)
        {
            Name = name;
            CycleSemaphore = new SemaphoreSlim(1, 1);
        }

        public string Name { get; }
        public SemaphoreSlim CycleSemaphore { get; }

        public GateState State => (GateState)Volatile.Read(ref _state);

        public void SetState(GateState state)
        {
            Volatile.Write(ref _state, (int)state);
        }
    }
}
