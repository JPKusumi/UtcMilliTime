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
            // ********************     // Crib this line-
            ITime Time = Clock.Time;
            // ********************     // Local variable 'Time' now has singleton instance reference
            Console.WriteLine("The instance of the UtcMilliTime clock has been instanciated.");
            Console.WriteLine();
            Console.WriteLine($"At this point, initialized = {Time.Initialized} (should be true), but synchronized = {Time.Synchronized} (should be false). ");
            Console.WriteLine($" Meaning, the clock has the device local time, but not network time.");
            Console.WriteLine(" (We've got 'outbound radio silence' by default.)");
            Console.WriteLine();
            Console.WriteLine("When you press enter, it will be given permission to use the network.");
            Console.ReadLine();
            // ********************     // Crib this line-
            Time.SuppressNetworkCalls = false;
            // ********************     // Meaning, the clock can break its 'radio silence'.

            // ********************     // Now an optional line-
            Time.NetworkTimeAcquired += Time_NetworkTimeAcquired;
            // ********************     // That subscribed / added an event handler for notification when sync up occurs.
            Console.WriteLine("Okay. A call may be made to an NTP server now, subject to network availability.");
            Console.WriteLine("Pressing enter now will interrogate the clock about the current time.");
            Console.ReadLine();
            // ********************     // You will crib this line many times-
            var timestamp = Time.Now;   // long integer
            // ********************     // That's standard usage. All remaining code lines are optional.

            // Backstory:
            var networkTime = Time.Synchronized;    // boolean
            var theServer = Time.DefaultServer;     // string
            var skew = Time.Skew;                   // long integer
            Console.WriteLine($"Q. What time is it? A. {timestamp}  Unix time (seconds): {timestamp.ToUnixTime()}  Milliseconds: {timestamp.MillisecondPart()}");
            Console.WriteLine();
            // Conversion to .NET's DateTime type, two examples:
            Console.WriteLine($"Q. What is that in human readable form? A. {timestamp.ToUtcDateTime()} in UTC time zone +{timestamp.MillisecondPart()}ms");
            Console.WriteLine();
            Console.WriteLine($"Q. ...in local time? A. Adjusted to your local settings, it's {timestamp.ToLocalDateTime()} +{timestamp.MillisecondPart()}ms");
            Console.WriteLine();
            Console.WriteLine($"Q. Show UTC time in ISO-8601 format? A. {timestamp.ToIso8601String()} ...Q....without milliseconds? A. {timestamp.ToIso8601String(true)}");
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
            Console.WriteLine(" =====  Press enter to quit the Runner program  =====");
            Console.ReadLine();
        }
        private static void Time_NetworkTimeAcquired(object sender, NTPEventArgs e)
        {
            // This is the optional event handler.
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
