For a source of timestamps, you can use UtcMilliTime.dll, a less-than-20KB library. Install the NuGet package or otherwise set a reference to the dll. Add

using UtcMilliTime;

to the top of the code file where you will use it. At class level or early in your code, say-

ITime Time = Clock.Time;

or you may prefer to use dependency injection which would allow you to mock the ITime interface. Then, in the normal course of your code, you can say:

var timestamp = Time.Now;

That will get you an Int64 (long) timestamp value expressing the whole number of milliseconds that have elapsed in the Unix Epoch since 1/1/1970 00:00:00, excluding leap seconds. Also, just once at the beginning of run time, you should use this line-

Time.SuppressNetworkCalls = false;

By default, the component would pass along device time - the time set on the local device by Windows and perhaps the user. The line above gives the clock permission to use the network, for synchronization with an NTP (Network Time Protocol) server. The clock then updates itself (asynchronously) to network time, and it leaves device time alone. Device time will be ignored for the rest of run time.

(However, if permission is not given as above, then the clock yields device time, and network time will be ignored.)

For more info, see the ReadMe at https://github.com/JPKusumi/UtcMilliTime