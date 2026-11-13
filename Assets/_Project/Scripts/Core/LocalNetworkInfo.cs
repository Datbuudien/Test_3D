using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

// IPv4 LAN cua may nay -> man Setup hien cho may thu 2 nhap vao o IP khi Join (test 2 may, N6)
public static class LocalNetworkInfo
{
    private const string ADDRESS_SEPARATOR = ", ";

    public static string GetLanIPv4Addresses()
    {
        List<string> addresses = new List<string>();
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsUsable(networkInterface))
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicast.Address.ToString());
                    }
                }
            }
        }
        catch (Exception exception) when (exception is NetworkInformationException || exception is PlatformNotSupportedException)
        {
            return string.Empty;
        }

        return string.Join(ADDRESS_SEPARATOR, addresses);
    }

    private static bool IsUsable(NetworkInterface networkInterface)
    {
        return networkInterface.OperationalStatus == OperationalStatus.Up
            && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
            && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }
}
