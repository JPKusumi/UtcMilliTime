namespace UtcMilliTime
{
    using System;
    public class NTPEventArgs : EventArgs
    {
        public string Server { get; }
        public long Latency { get; }
        public long Skew { get; }
        public NTPEventArgs(string server, long latency, long skew)
        {
            Server = server;
            Latency = latency;
            Skew = skew;
        }
    }
}
