# UtcMilliTime takes Windows.ToUnixTimeMilliseconds()
UtcMilliTime is a C# time component (software defined clock) for Windows that yields UnixTimeMilliseconds Int64 timestamps. .NET developers can use Time.Now just as JavaScript devs use Date.now(). Synchronizes with NTP (Network Time Protocol). Mock friendly.

## UtcMilliTime
**A C# clock class** that returns `Int64` timestamps of format—
```
Unix time * 1000 + milliseconds
```
On Windows systems, you can get timestamps from **UtcMilliTime's Clock class**.

### What kind of timestamps?

The stamps resemble Unix time (a whole number of seconds in the Unix epoch) with three extra digits that express milliseconds. The format expresses time as a single integer; the whole number is the raw count of the number of milliseconds elapsed during the Unix epoch - from 1/1/1970 00:00:00 to the present (albeit, ignoring leap seconds, which is the standard for Unix time). The single integer will be of type long (the C# keyword for `Int64`). As a signed integer, it permits negative numbers, which allows the expression of dates prior to 1970.

Due to internals of the Windows operating system, the available value only updates at intervals of 10 - 16 ms. (Apparently, updates rely on a message pump within Windows, running at a pace of 60 - 100 frames per second.)

With that said, you can get timestamps from **UtcMilliTime's Clock class**. For server side and database developers, we appreciate storing UTC (unambiguous) time in a single 64-bit integer. For machine-to-machine communication, this is a good format of timestamps. -This format is the normal output of JavaScript's `Date.now()` function. Localization (adjustment for time zone and daylight savings time) can happen on the client side along with formatting to display times to end users.

Note that Unix time has a Year 2038 problem; 32-bit signed integers will overflow (wrap around) then, as Unix time increases by 86400 seconds per day. By using 64-bit integers, **UtcMilliTime's Clock class** avoids the Year 2038 problem.

### Bypassing device time

It may also be said that **UtcMilliTime** is a software defined clock. It may initialize with device time, but if connectivity and permission are present it will make its own call to an NTP (Network Time Protocol) server to synchronize itself with network time. After it retrieves network time, note that it does not adjust device time. From then onwards, it simply ignores device time.

Device time relies on the user-changeable time settings of the local device. User settings do not always pass a sanity check and can be an attack vector; therefore, we take network time to be more accurate and reliable.

### Technical note

The software defined clock uses this definition of "now":
```
device_boot_time + device_uptime
```
This number goes up even during a leap second, when Unix time counting pauses for a second. After that, UtcMilliTime will appear to be one second fast, ahead of Unix time. If a leap second has happened during run time, and you don't want to restart the process or the device, your code can prompt re-synchronization by calling `Time.SelfUpdateAsync()`. To that method, you can optionally pass a string parameter with the host name of a particular NTP time server.

If connectivity is absent, or the host name is misspelled, etc. (various problems can occur), the call to `Time.SelfUpdateAsync()` will fail silently. Check the `Time.Synchronized` boolean property for the outcome.

### General usage

Add a reference from your project to UtcMilliTime.dll, and then in your code file, add this using statement—
```
using UtcMilliTime;
```
With no further preliminaries, you can code—
```
var timestamp = Clock.Time.Now;
```
However, a recommended preliminary step is to add this line—
```
ITime Time = Clock.Time;
```
Then you have a Time variable that references UtcMilliTime (which is a singleton instance). Now you can shorten your code thusly—
```
var timestamp = Time.Now;
```
Another preliminary is to add this line—
```
Time.SuppressNetworkCalls = false;
```
By default, the clock initializes with device time and leaves the network alone. The above line of code "gives permission" for UtcMilliTime to use the network for synchronization, via Network Time Protocol (NTP) to network time. The setting is durable for the rest of runtime. (The line needs only to execute once.)

With that permission, and subject to connectivity, the clock will synchronize itself to network time.

If your code would like to be notified when synchronization occurs, you can subscribe to an event, `NetworkTimeAcquired`. Here is a line of code which subscribes—
```
Time.NetworkTimeAcquired += Time_NetworkTimeAcquired;
```
Then you would actually handle the event in a method like this:
```
private static void Time_NetworkTimeAcquired(object sender, NTPEventArgs e)
{
  // The clock just synced up to network time. Place your code for the
  // occasion here. At this point, e.Skew is an interesting value. It
  // expresses the difference between device time and network time.
  //      var networkTimestamp = oldDeviceTimestamp + e.Skew;
}
```
