namespace Runner
{
    using System;
    using UtcMilliTime;
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(" =====  Runner program starting  =====");
            Console.WriteLine();
            ITime Time = Clock.Time;    // Local variable 'Time' now has singleton instance reference
            Console.WriteLine("The instance of the UtcMilliTime clock has been instanciated.");
            Console.WriteLine();
            Console.WriteLine($"At this point, initialized = {Time.Initialized} (should be True), but synchronized = {Time.Synchronized} (should be False). ");
            Console.WriteLine($" Meaning, the clock has the device local time, but not network time.");
            Console.WriteLine(" (We've got 'outbound radio silence' by default.)");
            Console.WriteLine();
            Console.WriteLine("When you press enter, it will be given permission to use the network.");
            Console.ReadLine();
            Time.NetworkTimeAcquired += Time_NetworkTimeAcquired; // Added event handler to be notified upon sync up.
            Console.WriteLine("Okay. A call may be made to an NTP server now, subject to network availability.");
            Console.WriteLine("Pressing enter now will interrogate the clock about the current time.");
            Time.SuppressNetworkCalls = false; // The clock may break its 'radio silence'.
            Console.ReadLine();
            var timestamp = Time.Now;   // This is a line you will use many times.
            var networkTime = Time.Synchronized;
            var theServer = Time.DefaultServer;
            var skew = Time.Skew;
            Console.WriteLine($"Q. What time is it? A. {timestamp}  Unix time (seconds): {timestamp.ToUnixTime()}  Milliseconds: {timestamp.MillisecondPart()}");
            Console.WriteLine();
            Console.WriteLine($"Q. What is that in ISO-8601 format? A. {timestamp.ToIso8601String()}");
            Console.WriteLine();
            Console.WriteLine($"Q. ...without milliseconds? A. {timestamp.ToIso8601String(true)}");
            Console.WriteLine();
            Console.WriteLine($"Q. What is that in human readable form? A. {timestamp.ToUtcDateTime()} in UTC time zone +{timestamp.MillisecondPart()}ms");
            Console.WriteLine();
            Console.WriteLine($"Q. ...in local time? A. Adjusted to your local settings, it's {timestamp.ToLocalDateTime()} +{timestamp.MillisecondPart()}ms");
            Console.WriteLine();
            Console.WriteLine($"Q. What is the source of that time? A. {(networkTime ? "Network time from " + theServer : "Device time from the local system's .NET/Microsoft components")}");
            Console.WriteLine();
            if (networkTime)
            {
                // Skew analysis:
                string answer = (skew < 0) ? $"Device time is ahead of network time by {-skew} milliseconds" : $"Device time is behind network time by {skew} milliseconds";
                Console.WriteLine($"Q. How far off is device time from network time? A. {answer}");
            }
            Console.WriteLine();
            Console.WriteLine(" ===== Press enter for more trivia  =====");
            Console.ReadLine();
            Console.WriteLine($"You booted your device at {Time.DeviceBootTime.ToLocalDateTime()}");
            Console.WriteLine();
            var uptime = Time.DeviceUpTime;
            Console.WriteLine($"The device has been running for {uptime} milliseconds.");
            Console.WriteLine();
            Console.WriteLine($"That is also {uptime.IntervalDays()} days, {uptime.IntervalHoursPart()} hours, {uptime.IntervalMinutesPart()} minutes, {uptime.IntervalSecondsPart()} seconds, and {uptime.MillisecondPart()} milliseconds.");
            Console.WriteLine();
            Console.WriteLine(" =====  End of Runner program  =====");
        }
        private static void Time_NetworkTimeAcquired(object sender, NTPEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine($"      - Network time has been acquired. -");
            Console.WriteLine($"      - Server used: {e.Server} -");
            Console.WriteLine($"      - Round trip latency: {e.Latency} ms -");
            Console.WriteLine($"      - Skew measured: {e.Skew} ms -");
            Console.WriteLine();
            Console.WriteLine("Pressing enter now will interrogate the clock about the current time.");
        }
    }
}
