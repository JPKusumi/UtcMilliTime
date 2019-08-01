namespace UtcMilliTime
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    public sealed class Clock : ITime
    {
        private static readonly Lazy<Clock> instance = new Lazy<Clock>(() => new Clock());
        public static Clock Time => instance.Value;
        [System.Runtime.InteropServices.DllImport("kernel32")]
        extern static ulong GetTickCount64();
        private static long device_boot_time;
        private static bool successfully_synced;
        private bool suppress_network_calls = true;
        public string DefaultServer { get; set; } = Constants.fallback_server;
        public long DeviceBootTime => device_boot_time;
        public long DeviceUpTime => (long)GetTickCount64();
        public long DeviceUtcNow => GetDeviceTime();
        public bool Initialized => device_boot_time != 0;
        public long Now => device_boot_time + (long)GetTickCount64();
        public long Skew { get; private set; }
        public bool SuppressNetworkCalls
        {
            get
            {
                return suppress_network_calls;
            }
            set
            {
                if (value != suppress_network_calls)
                {
                    suppress_network_calls = value;
                    if (!successfully_synced && ThereIsConnectivity && !suppress_network_calls) SelfUpdateAsync().SafeFireAndForget(false);
                }
            }
        }
        public bool Synchronized => successfully_synced;
        public event EventHandler<NTPEventArgs> NetworkTimeAcquired;
        private static bool ThereIsConnectivity => NetworkInterface.GetIsNetworkAvailable();
        private Clock()
        {
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            if (ThereIsConnectivity)
            {
                SelfUpdateAsync().SafeFireAndForget(false);
                return;
            }
            Initialize();
        }
        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (!successfully_synced && e.IsAvailable && !suppress_network_calls) SelfUpdateAsync().SafeFireAndForget(false);
        }
        private void Initialize()
        {
            device_boot_time = GetDeviceTime() - (long)GetTickCount64();
            successfully_synced = false;
            Skew = 0;
        }
        private static long GetDeviceTime() => (DateTime.UtcNow.Ticks / 10000) - Constants.dotnet_to_unix_milliseconds;
        public async Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server)
        {
            bool prior_sync_state = successfully_synced;
            Initialize();
            if (!Initialized || !ThereIsConnectivity || suppress_network_calls) return;
            if (ntpServerHostName == Constants.fallback_server && !string.IsNullOrEmpty(DefaultServer)) ntpServerHostName = DefaultServer;
            Stopwatch latency = Stopwatch.StartNew();
            long timeNow = await Task.Run(() => GetNetworkTime(ntpServerHostName)).ConfigureAwait(false);
            latency.Stop();
            if (timeNow == 0) return;
            Skew = timeNow - GetDeviceTime();
            device_boot_time = timeNow - (long)GetTickCount64();
            successfully_synced = true;
            if (!prior_sync_state)
            {
                NTPEventArgs args = new NTPEventArgs(ntpServerHostName, latency.ElapsedMilliseconds, Skew);
                NetworkTimeAcquired?.Invoke(this, args);
            }
        }
        private static long GetNetworkTime(string forNtpServer)
        {
            var ntpData = new byte[48];
            ntpData[0] = 0x1B;
            long halfRoundTrip;
            try
            {
                var addresses = Dns.GetHostEntry(forNtpServer).AddressList;
                var ipEndPoint = new IPEndPoint(addresses[0], 123);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(ipEndPoint);
                    socket.ReceiveTimeout = 3000;
                    Stopwatch timer = Stopwatch.StartNew();
                    socket.Send(ntpData);
                    socket.Receive(ntpData);
                    timer.Stop();
                    halfRoundTrip = timer.ElapsedMilliseconds / 2;
                    socket.Close();
                }
            }
            catch (Exception)
            {
                return 0;
            }
            const byte serverReplyTime = 40;
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);
            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            return (long)milliseconds - Constants.ntp_to_unix_milliseconds + halfRoundTrip;
        }
        private static uint SwapEndianness(ulong x) => (uint)(((x & 0x000000ff) << 24) +
                   ((x & 0x0000ff00) << 8) +
                   ((x & 0x00ff0000) >> 8) +
                   ((x & 0xff000000) >> 24));
    }
}
