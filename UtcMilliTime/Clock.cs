namespace UtcMilliTime
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    public sealed class Clock : ITime
    {
        private static readonly Lazy<Clock> instance = new Lazy<Clock>(() => new Clock());
        public static Clock Time => instance.Value;
        [System.Runtime.InteropServices.DllImport("kernel32")]
        extern static ulong GetTickCount64();
        private static long device_uptime => (long)GetTickCount64();
        private static long device_boot_time;
        private static bool successfully_synced;
        private static bool suppress_network_calls = true;
        private static bool reentrant;
        private static bool prior_sync_state;
        private static Stopwatch latency;
        private static Stopwatch timer;
        private static long halfRoundTrip;
        private static byte[] ntpBuffer = new byte[48];
        private static long retrievedTime;
        private static long _skew;
        private static string server_resolved;
        public string DefaultServer { get; set; } = Constants.fallback_server;
        public long DeviceBootTime => device_boot_time;
        public long DeviceUpTime => device_uptime;
        public long DeviceUtcNow => GetDeviceTime();
        public bool Initialized => device_boot_time != 0;
        public long Now => device_boot_time + device_uptime;
        public long Skew { get => _skew; }
        public bool SuppressNetworkCalls
        {
            get => suppress_network_calls;
            set
            {
                if (value != suppress_network_calls)
                {
                    suppress_network_calls = value;
                    if (Indicated) SelfUpdateAsync().SafeFireAndForget(false);
                }
            }
        }
        public bool Synchronized => successfully_synced;
        public event EventHandler<NTPEventArgs> NetworkTimeAcquired;
        private static bool Indicated => !suppress_network_calls && !successfully_synced && NetworkInterface.GetIsNetworkAvailable();
        private Clock()
        {
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            if (Indicated) SelfUpdateAsync().SafeFireAndForget(false); else Initialize();
        }
        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (Indicated) SelfUpdateAsync().SafeFireAndForget(false);
        }
        private void Initialize()
        {
            device_boot_time = GetDeviceTime() - device_uptime;
            successfully_synced = false;
            _skew = 0;
        }
        private static long GetDeviceTime() => DateTime.UtcNow.Ticks / Constants.dotnet_ticks_per_millisecond - Constants.dotnet_to_unix_milliseconds;
        public async Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server)
        {
            if (reentrant) return; else reentrant = true;
            prior_sync_state = successfully_synced;
            Initialize();
            if (!Initialized || !Indicated) 
            {
                reentrant = false;
                return;
            }
            if (ntpServerHostName == Constants.fallback_server && !string.IsNullOrEmpty(DefaultServer)) ntpServerHostName = DefaultServer;
            server_resolved = ntpServerHostName;
            latency = Stopwatch.StartNew();
            ntpBuffer.Initialize();
            ntpBuffer[0] = 0x1B;
            halfRoundTrip = 0;
            retrievedTime = 0;
            var NTPsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                var ipEndPoint = new IPEndPoint(Dns.GetHostAddresses(ntpServerHostName)[0], Constants.udp_port_number);
                NTPsocket.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), NTPsocket);
            }
            catch (Exception)
            {
                NTPsocket.Shutdown(SocketShutdown.Both);
                NTPsocket.Close();
                latency.Stop();
                reentrant = false;
                return;
            }
        }
        private static void ConnectCallback(IAsyncResult ar)
        {
            var theSocket = (Socket)ar.AsyncState;
            theSocket.EndConnect(ar);
            theSocket.ReceiveTimeout = Constants.three_seconds;
            timer = Stopwatch.StartNew();
            try
            {
                theSocket.BeginSend(ntpBuffer, 0, 48, 0, new AsyncCallback(SendCallback), theSocket);
            }
            catch (Exception)
            {
                timer.Stop();
                theSocket.Shutdown(SocketShutdown.Both);
                theSocket.Close();
                latency.Stop();
                reentrant = false;
                return;
            }
        }
        private static void SendCallback(IAsyncResult ar)
        {
            var theSocket = (Socket)ar.AsyncState;
            theSocket.EndSend(ar);
            try
            {
                theSocket.BeginReceive(ntpBuffer, 0, 48, 0, new AsyncCallback(ReceiveCallback), theSocket);
            }
            catch (Exception)
            {
                timer.Stop();
                theSocket.Shutdown(SocketShutdown.Both);
                theSocket.Close();
                latency.Stop();
                reentrant = false;
                return;
            }
        }
        private static void ReceiveCallback(IAsyncResult ar)
        {
            var theSocket = (Socket)ar.AsyncState;
            theSocket.EndReceive(ar);
            timer.Stop();
            halfRoundTrip = timer.ElapsedMilliseconds / 2;
            const byte serverReplyTime = 40;
            ulong intPart = BitConverter.ToUInt32(ntpBuffer, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpBuffer, serverReplyTime + 4);
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);
            var milliseconds = intPart * 1000 + fractPart * 1000 / 0x100000000L;
            retrievedTime = (long)milliseconds - Constants.ntp_to_unix_milliseconds + halfRoundTrip;
            latency.Stop();
            if (milliseconds == 0) 
            {
                theSocket.Shutdown(SocketShutdown.Both);
                theSocket.Close();
                reentrant = false;
                return;
            }
            _skew = retrievedTime - GetDeviceTime();
            device_boot_time = retrievedTime - device_uptime;
            successfully_synced = true;
            if (!prior_sync_state && instance.Value.NetworkTimeAcquired != null)
            {
                NTPEventArgs args = new NTPEventArgs(server_resolved, latency.ElapsedMilliseconds, _skew);
                instance.Value.NetworkTimeAcquired.Invoke(new object(), args);
            }
            theSocket.Shutdown(SocketShutdown.Both);
            theSocket.Close();
            reentrant = false;
        }
        private static uint SwapEndianness(ulong x) => (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
    }
}
