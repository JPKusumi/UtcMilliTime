namespace UtcMilliTime
{
    public static class Constants
    {
        public const short three_seconds = 3000;
        public const short udp_port_number = 123;
        public const short second_milliseconds = 1000;
        public const short dotnet_ticks_per_millisecond = 10000;
        public const int minute_milliseconds = 60000;
        public const int hour_milliseconds = 3600000;
        public const int day_milliseconds = 86400000;
        public const long ntp_to_unix_milliseconds = 2208988800000;
        public const long dotnet_to_unix_milliseconds = 62135596800000;
        public const string fallback_server = "time.google.com";
    }
}
