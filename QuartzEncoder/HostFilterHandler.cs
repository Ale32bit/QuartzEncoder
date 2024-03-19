using System.Net;

namespace QuartzEncoder;

public class HostFilterHandler
{
    public static async Task<IPAddress?> ResolveAddress(Uri uri)
    {
        if (IPAddress.TryParse(uri.Host, out var address))
            return address;

        var addresses = await Dns.GetHostAddressesAsync(uri.Host);
        return addresses.FirstOrDefault();
    }

    public static async Task<bool> IsLocalhostOrPrivateNetwork(Uri uri)
    {
        var address = await ResolveAddress(uri);
        if (address == null) return false;
        if (IPAddress.IsLoopback(address) || IsPrivateIpAddress(address))
        {
            return true;
        }

        return false;
    }

    public static bool IsPrivateIpAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
