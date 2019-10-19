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
        private static NTPCallState ntpCall;
        public string DefaultServer { get; set; } = Constants.fallback_server;
        public long DeviceBootTime => device_boot_time;
        public long DeviceUpTime => device_uptime;
        public long DeviceUtcNow => GetDeviceTime();
        public bool Initialized => device_boot_time != 0;
        public long Now => device_boot_time + device_uptime;
        public long Skew { get; private set; }
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
            Skew = 0;
        }
        private static long GetDeviceTime() => DateTime.UtcNow.Ticks / Constants.dotnet_ticks_per_millisecond - Constants.dotnet_to_unix_milliseconds;
        public async Task SelfUpdateAsync(string ntpServerHostName = Constants.fallback_server)
        {
            if (ntpCall != null) return;
            ntpCall = new NTPCallState
            {
                priorSyncState = successfully_synced
            };
            Initialize();
            if (!Initialized || !Indicated)
            {
                ntpCall = null;
                return;
            }
            if (ntpServerHostName == Constants.fallback_server && !string.IsNullOrEmpty(DefaultServer)) ntpServerHostName = DefaultServer;
            ntpCall.serverResolved = ntpServerHostName;
            var ntpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                var ipEndPoint = new IPEndPoint(Dns.GetHostAddresses(ntpServerHostName)[0], Constants.udp_port_number);
                ntpSocket.BeginConnect(ipEndPoint, new AsyncCallback(Chapter2), ntpSocket);
                ntpCall.methodsCompleted += 1;
            }
            catch (Exception)
            {
                ntpSocket.Shutdown(SocketShutdown.Both);
                ntpSocket.Close();
                ntpCall.latency.Stop();
                ntpCall = null;
                return;
            }
        }
        private static void Chapter2(IAsyncResult ar)
        {
            var ntpSocket = (Socket)ar.AsyncState;
            ntpSocket.EndConnect(ar);
            ntpSocket.ReceiveTimeout = Constants.three_seconds;
            try
            {
                if (ntpCall == null)
                {
                    ntpSocket.Shutdown(SocketShutdown.Both);
                    ntpSocket.Close();
                    return;
                }
                ntpCall.timer = Stopwatch.StartNew();
                ntpSocket.BeginSend(ntpCall.buffer, 0, 48, 0, new AsyncCallback(Chapter3), ntpSocket);
                ntpCall.methodsCompleted += 1;
            }
            catch (Exception)
            {
                ntpCall.timer.Stop();
                ntpSocket.Shutdown(SocketShutdown.Both);
                ntpSocket.Close();
                ntpCall.latency.Stop();
                ntpCall = null;
                return;
            }
        }
        private static void Chapter3(IAsyncResult ar)
        {
            var ntpSocket = (Socket)ar.AsyncState;
            ntpSocket.EndSend(ar);
            try
            {
                if (ntpCall == null)
                {
                    ntpSocket.Shutdown(SocketShutdown.Both);
                    ntpSocket.Close();
                    return;
                }
                ntpSocket.BeginReceive(ntpCall.buffer, 0, 48, 0, new AsyncCallback(Chapter4), ntpSocket);
                ntpCall.methodsCompleted += 1;
            }
            catch (Exception)
            {
                ntpCall.timer.Stop();
                ntpSocket.Shutdown(SocketShutdown.Both);
                ntpSocket.Close();
                ntpCall.latency.Stop();
                ntpCall = null;
                return;
            }
        }
        private static void Chapter4(IAsyncResult ar)
        {
            var ntpSocket = (Socket)ar.AsyncState;
            ntpSocket.EndReceive(ar);
            if (ntpCall == null)
            {
                ntpSocket.Shutdown(SocketShutdown.Both);
                ntpSocket.Close();
                return;
            }
            ntpCall.timer.Stop();
            long halfRoundTrip = ntpCall.timer.ElapsedMilliseconds / 2;
            const byte serverReplyTime = 40;
            ulong intPart = BitConverter.ToUInt32(ntpCall.buffer, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpCall.buffer, serverReplyTime + 4);
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);
            var milliseconds = intPart * 1000 + fractPart * 1000 / 0x100000000L;
            long timeNow = (long)milliseconds - Constants.ntp_to_unix_milliseconds + halfRoundTrip;
            if (timeNow <= 0) 
            {
                ntpSocket.Shutdown(SocketShutdown.Both);
                ntpSocket.Close();
                ntpCall = null;
                return;
            }
            instance.Value.Skew = timeNow - GetDeviceTime();
            device_boot_time = timeNow - device_uptime;
            ntpCall.methodsCompleted += 1;
            successfully_synced = ntpCall.methodsCompleted == 4;
            ntpCall.latency.Stop();
            if (successfully_synced && !ntpCall.priorSyncState && instance.Value.NetworkTimeAcquired != null)
            {
                NTPEventArgs args = new NTPEventArgs(ntpCall.serverResolved, ntpCall.latency.ElapsedMilliseconds, instance.Value.Skew);
                instance.Value.NetworkTimeAcquired.Invoke(new object(), args);
            }
            ntpSocket.Shutdown(SocketShutdown.Both);
            ntpSocket.Close();
            ntpCall = null;
        }
        private static uint SwapEndianness(ulong x) => (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
    }
}
