using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace Miside.Camera.Desktop;

internal static class UsbNetwork
{
    private const string NetworkKey = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";

    internal static bool IsUsbDevice(string? instance) => instance?.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) == true;

    public static string Addresses()
    {
        var addresses = new List<string>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
            if (adapter.OperationalStatus != OperationalStatus.Up) continue;
            using var connection = Registry.LocalMachine.OpenSubKey(NetworkKey + @"\" + adapter.Id + @"\Connection");
            if (!IsUsbDevice(connection?.GetValue("PnpInstanceID") as string)) continue;
            try {
                addresses.AddRange(adapter.GetIPProperties().UnicastAddresses
                    .Where(value => value.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(value => adapter.Name + " · " + value.Address));
            } catch (NetworkInformationException) {
                // USB may be unplugged after enumeration; the next refresh reflects removal.
            }
        }
        return string.Join(Environment.NewLine, addresses);
    }
}

public sealed class ConnectionModeOption(string code, string resourceKey) : Observable
{
    public string Code { get; } = code;
    public string Name => Localization.T(resourceKey);
    public void Refresh() => Changed(nameof(Name));
}
