namespace UtcMilliTime
{
    using System;
    using System.Threading.Tasks;
    public interface ITime
    {
        string DefaultServer { get; set; }
        long DeviceBootTime { get; }
        long DeviceUpTime { get; }
        long DeviceUtcNow { get; }
        bool Initialized { get; }
        long Now { get; }
        long Skew { get; }
        bool SuppressNetworkCalls { get; set; }
        bool Synchronized { get; }

        event EventHandler<NTPEventArgs> NetworkTimeAcquired;

        Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server);
    }
}