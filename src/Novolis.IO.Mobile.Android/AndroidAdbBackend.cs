using System.Text;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Narrow protocol seam used by <see cref="AndroidDebugBridge"/>.
/// Implementations can be replaced by deterministic fakes in tests.
/// </summary>
public interface IAndroidAdbBackend
{
    /// <summary>Lists devices known by the local adb server.</summary>
    IReadOnlyList<AdbDevice> ListDevices();

    /// <summary>Lists devices asynchronously.</summary>
    Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a command against one resolved serial.</summary>
    AdbProcessResult Shell(string command, string serial);

    /// <summary>Runs a command asynchronously against one resolved serial.</summary>
    Task<AdbProcessResult> ShellAsync(
        string command,
        string serial,
        CancellationToken cancellationToken = default);

    /// <summary>Installs one APK.</summary>
    AdbOperationResult Install(string apkPath, string serial, string[] installArguments);

    /// <summary>Installs one APK asynchronously.</summary>
    Task<AdbOperationResult> InstallAsync(
        string apkPath,
        string serial,
        string[] installArguments,
        CancellationToken cancellationToken = default);

    /// <summary>Uninstalls a package.</summary>
    AdbOperationResult Uninstall(string packageName, string serial);

    /// <summary>Uninstalls a package asynchronously.</summary>
    Task<AdbOperationResult> UninstallAsync(
        string packageName,
        string serial,
        CancellationToken cancellationToken = default);

    /// <summary>Pushes a local file.</summary>
    AdbOperationResult Push(string localPath, string remotePath, string serial);

    /// <summary>Pushes a local file asynchronously.</summary>
    Task<AdbOperationResult> PushAsync(
        string localPath,
        string remotePath,
        string serial,
        CancellationToken cancellationToken = default);

    /// <summary>Pulls a remote file.</summary>
    AdbOperationResult Pull(string remotePath, string localPath, string serial);

    /// <summary>Pulls a remote file asynchronously.</summary>
    Task<AdbOperationResult> PullAsync(
        string remotePath,
        string localPath,
        string serial,
        CancellationToken cancellationToken = default);
}

/// <summary>ADB protocol implementation backed by AdvancedSharpAdbClient.</summary>
public sealed class ProtocolAndroidAdbBackend : IAndroidAdbBackend
{
    private readonly AdbClient _client;

    /// <summary>Creates a protocol backend.</summary>
    public ProtocolAndroidAdbBackend(AdbClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public IReadOnlyList<AdbDevice> ListDevices() =>
        _client.GetDevices().Select(MapDevice).ToArray();

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _client.GetDevicesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        return devices.Select(MapDevice).ToArray();
    }

    /// <inheritdoc />
    public AdbProcessResult Shell(string command, string serial) =>
        ShellAsync(command, serial).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AdbProcessResult> ShellAsync(
        string command,
        string serial,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var device = RequireDevice(serial);
        var marker = $"__NOVOLIS_EXIT_{Guid.NewGuid():N}__";
        var wrapped = $"{{ {command}; printf '\\n{marker}:%s\\n' $?; }}";
        var receiver = new ConsoleOutputReceiver();
        try
        {
            await _client.ExecuteRemoteCommandAsync(
                    wrapped,
                    device,
                    receiver,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AdbProcessResult(1, "", "ADB shell operation cancelled.")
            {
                Cancelled = true,
            };
        }
        catch (Exception ex)
        {
            return new AdbProcessResult(1, "", ex.Message);
        }

        var output = receiver.ToString() ?? "";
        var markerIndex = output.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return new AdbProcessResult(0, output, "");

        var statusStart = markerIndex + marker.Length + 1;
        var lineEnd = output.IndexOfAny(['\r', '\n'], statusStart);
        if (lineEnd < 0)
            lineEnd = output.Length;
        var statusText = output[statusStart..lineEnd].Trim();
        var body = output[..markerIndex].TrimEnd('\r', '\n');
        if (!int.TryParse(statusText, out var exitCode))
            exitCode = 0;

        return new AdbProcessResult(
            exitCode,
            body,
            exitCode == 0 ? "" : $"Remote shell command exited with code {exitCode}.")
        {
            Truncated = false,
        };
    }

    /// <inheritdoc />
    public AdbOperationResult Install(
        string apkPath,
        string serial,
        string[] installArguments) =>
        InstallAsync(apkPath, serial, installArguments).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AdbOperationResult> InstallAsync(
        string apkPath,
        string serial,
        string[] installArguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = RequireDevice(serial);
            await using var stream = File.OpenRead(apkPath);
            await _client.InstallAsync(
                    device,
                    stream,
                    callback: null,
                    cancellationToken: cancellationToken,
                    arguments: installArguments)
                .ConfigureAwait(false);
            return AdbOperationResult.Success(
                "install",
                $"Installed {Path.GetFileName(apkPath)}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "install",
                "APK installation cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "install",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <inheritdoc />
    public AdbOperationResult Uninstall(string packageName, string serial) =>
        UninstallAsync(packageName, serial).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AdbOperationResult> UninstallAsync(
        string packageName,
        string serial,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = RequireDevice(serial);
            await _client.UninstallAsync(device, packageName, cancellationToken).ConfigureAwait(false);
            return AdbOperationResult.Success("uninstall", $"Uninstalled {packageName}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                "Package uninstall cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <inheritdoc />
    public AdbOperationResult Push(string localPath, string remotePath, string serial) =>
        PushAsync(localPath, remotePath, serial).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AdbOperationResult> PushAsync(
        string localPath,
        string remotePath,
        string serial,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = RequireDevice(serial);
            await using var source = File.OpenRead(localPath);
            using var sync = new SyncService(_client, device);
            await sync.PushAsync(
                    source,
                    remotePath,
                    UnixFileStatus.DefaultFileMode,
                    DateTimeOffset.UtcNow,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return AdbOperationResult.Success("push", $"Pushed → {remotePath}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "push",
                "File push cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "push",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <inheritdoc />
    public AdbOperationResult Pull(string remotePath, string localPath, string serial) =>
        PullAsync(remotePath, localPath, serial).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AdbOperationResult> PullAsync(
        string remotePath,
        string localPath,
        string serial,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = RequireDevice(serial);
            var full = Path.GetFullPath(localPath);
            var directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var target = File.Create(full);
            using var sync = new SyncService(_client, device);
            await sync.PullAsync(remotePath, target, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return AdbOperationResult.Success("pull", $"Pulled → {full}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "pull",
                "File pull cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "pull",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    private DeviceData RequireDevice(string serial)
    {
        var device = _client
            .GetDevices()
            .FirstOrDefault(d => string.Equals(d.Serial, serial, StringComparison.OrdinalIgnoreCase));
        return device ?? throw new InvalidOperationException($"Device '{serial}' not found.");
    }

    private static AdbDevice MapDevice(DeviceData device) =>
        new(
            device.Serial ?? "",
            device.State switch
            {
                DeviceState.Online => AdbDeviceState.Device,
                DeviceState.Unauthorized => AdbDeviceState.Unauthorized,
                DeviceState.Offline => AdbDeviceState.Offline,
                DeviceState.NoPermissions => AdbDeviceState.NoPermissions,
                DeviceState.BootLoader => AdbDeviceState.Bootloader,
                DeviceState.Recovery => AdbDeviceState.Recovery,
                DeviceState.Sideload => AdbDeviceState.Sideload,
                _ => AdbDeviceState.Unknown,
            },
            NullIfEmpty(device.Product),
            NullIfEmpty(device.Model),
            NullIfEmpty(device.Name),
            NullIfEmpty(device.TransportId));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
