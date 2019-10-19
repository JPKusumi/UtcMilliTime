namespace UtcMilliTime
{
    using System.Diagnostics;
    public class NTPCallState
    {
        public bool priorSyncState;
        public Stopwatch latency;
        public Stopwatch timer;
        public byte[] buffer = new byte[48];
        public short methodsCompleted;
        public string serverResolved;
        public NTPCallState()
        {
            latency = Stopwatch.StartNew();
            buffer[0] = 0x1B;
        }
    }
}
