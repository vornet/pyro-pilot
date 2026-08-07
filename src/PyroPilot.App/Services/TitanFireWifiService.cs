using System.Diagnostics;
using System.Security;
using System.Text;

namespace PyroPilot.App.Services;

public interface ITitanFireWifiService
{
    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Installs a per-user Windows WLAN profile and connects to the access point.
/// Windows retains the profile and handles subsequent network reconnections.
/// </summary>
public sealed class TitanFireWifiService : ITitanFireWifiService
{
    public const string Ssid = "TitanFire";
    private const string Password = "66886688";

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException($"Automatic Wi-Fi setup is only available on Windows. Connect to '{Ssid}' manually.");

        string profilePath = Path.Combine(Path.GetTempPath(), $"PyroPilot-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(profilePath, CreateProfileXml(), new UTF8Encoding(false), cancellationToken);
            await RunNetshAsync(["wlan", "add", "profile", $"filename={profilePath}", "user=current"], cancellationToken);
            await RunNetshAsync(["wlan", "connect", $"name={Ssid}", $"ssid={Ssid}"], cancellationToken);

            // Give Windows a moment to associate and obtain the device-network address.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        finally
        {
            try { File.Delete(profilePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string CreateProfileXml()
    {
        string ssid = SecurityElement.Escape(Ssid)!;
        string password = SecurityElement.Escape(Password)!;
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{{ssid}}</name>
              <SSIDConfig><SSID><name>{{ssid}}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>auto</connectionMode>
              <MSM><security>
                <authEncryption>
                  <authentication>WPA2PSK</authentication>
                  <encryption>AES</encryption>
                  <useOneX>false</useOneX>
                </authEncryption>
                <sharedKey>
                  <keyType>passPhrase</keyType>
                  <protected>false</protected>
                  <keyMaterial>{{password}}</keyMaterial>
                </sharedKey>
              </security></MSM>
            </WLANProfile>
            """;
    }

    private static async Task RunNetshAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = await stdout;
        string error = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
    }
}
