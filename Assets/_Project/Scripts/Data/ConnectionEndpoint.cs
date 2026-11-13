using System.Net;
using System.Net.Sockets;

// Kiem tra IP/port nguoi dung nhap truoc khi goi mang. UnityTransport chi nhan chuoi IPv4 (khong phan giai ten mien).
public static class ConnectionEndpoint
{
    public static bool TryParseAddress(string rawAddress, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            return false;
        }

        string trimmed = rawAddress.Trim();
        // IPAddress.TryParse chap nhan ca "1" hay "1.2" -> bat buoc du 4 phan
        if (trimmed.Split('.').Length != 4
            || !IPAddress.TryParse(trimmed, out IPAddress parsed)
            || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        address = parsed.ToString();
        return true;
    }

    public static bool TryParsePort(string rawPort, out ushort port)
    {
        port = 0;
        return !string.IsNullOrWhiteSpace(rawPort)
            && ushort.TryParse(rawPort.Trim(), out port)
            && port > 0;
    }
}
