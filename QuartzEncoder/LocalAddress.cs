using System.Net;
using System.Net.Sockets;

namespace QuartzEncoder;


public static class LocalAddress
{
    public static bool IsLanAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }
        
        var bytes = ip.GetAddressBytes();

        return bytes[0] == 10 || // Class A
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // Class B
               (bytes[0] == 192 && bytes[1] == 168) || // Class C
               bytes[0] == 127; // Loopback
    }
}