<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Mobile.Android

Host-side Android Debug Bridge helpers for Novolis apps and dogfood tools.

Device work uses the **ADB wire protocol** via [AdvancedSharpAdbClient](https://www.nuget.org/packages/AdvancedSharpAdbClient/) (devices, shell, sync, install). The Android SDK `adb` / `adb.exe` binary is only required to **locate and ensure the local adb server**. A small, windowless CLI escape hatch is used for ADB-only verbs such as `screencap`, `logcat`, UIAutomator capture, and port forwarding.

This is **not** an on-device `net10.0-android` / MAUI package.

**Coverage:** this assembly is excluded from org line-coverage via `[assembly: ExcludeFromCodeCoverage]` (see `novolis-governance/docs/coverage-report.md`). Unit tests in `Novolis.IO.Unit` exercise parsers, target selection, redaction, and artifact validation; live ADB/device paths are validated by the `Adb` utility and `novolis-android`.

## Install

```bash
dotnet add package Novolis.IO.Mobile.Android
```

### Prerequisites

| Need | Detail |
|------|--------|
| SDK platform-tools | `adb` under `ANDROID_HOME` or `ANDROID_SDK_ROOT` `platform-tools/`, common default SDK paths, or `PATH` |
| USB debugging | Handset authorized for this PC (`adb devices` → `device`) |
| Existence check | `AdbLocator.Resolve` requires the file to exist before the server is started |

Optional: set `ANDROID_SERIAL` to pin a device when several are attached.

## Quick start

```csharp
using Novolis.IO.Mobile.Android;

var adb = new AndroidDebugBridge();
Console.WriteLine($"{adb.Transport} via {adb.AdbPath}"); // transport = "protocol"

foreach (var device in adb.ListDevices())
    Console.WriteLine($"{device.Serial}\t{device.State}\t{device.Model}");

var info = adb.GetDeviceInfo();
Console.WriteLine(info.FormatReport()); // identity, build, CPU, display, battery, RAM, storage
```

For cancellable host workflows:

```csharp
var ready = await adb.WaitForDeviceAsync(
    TimeSpan.FromSeconds(30),
    serial: null,
    cancellationToken: cancellationToken);
var logs = new AndroidDeviceDiagnostics(adb);
var capture = await logs.CaptureLogcatAsync(
    new AndroidLogcatOptions { Serial = ready.Serial, PackageName = "com.example.app" },
    cancellationToken);
```

## Architecture

```text
Your app ──► AndroidDebugBridge (Novolis façade)
                 │
                 ├─► AdvancedSharpAdbClient  ──TCP──► adb server (:5037) ──► USB / emulator
                 │
                 └─► AdbLocator / AdbServer.StartServer(adbPath)
                        (only to ensure the daemon is running)
```

| Piece | Role |
|-------|------|
| `AndroidDebugBridge` | Primary API: deterministic target selection, devices, props/stats, shell, sync, install/uninstall, start/stop |
| `AdbLocator` | Resolves `adb` with `File.Exists` |
| `ProcessAdbRunner` | Rare **CLI escape hatch** for binary, async, and ADB-only verbs |
| `ApkValidator` / `AndroidAppInstaller` | Validate APK → wait for device → install → verify / launch |
| `AndroidDeviceDiagnostics` | Logcat, screenshot, UI XML, input, forwarding, and redacted bundles |

## Device & stats

```csharp
adb.WaitForDevice(TimeSpan.FromSeconds(30));           // poll until State == Device
adb.GetState(serial);                                  // "device", …
adb.GetProp("ro.build.version.release");
adb.TryGetProp("ro.serialno");
var report = adb.GetDeviceInfo(serial).FormatReport();
```

`GetDeviceInfo` gathers identity, build/OS, ABI/CPU, display, battery, memory, storage, and a short `dumpsys display` excerpt (useful on foldables).
Before writing a report to a ticket or artifact, call
`AndroidOutputRedactor.RedactDeviceInfo(info)`; the CLI `info` command uses
this redacted form by default and requires `--raw` for local troubleshooting.

## Shell, files, packages

```csharp
var shell = adb.Shell("pm path com.example.app", serial);
adb.Push(@"D:\tmp\id.txt", "/data/local/tmp/id.txt", serial);
adb.Pull("/data/local/tmp/id.txt", @"D:\tmp\out.txt", serial);

var pkg = adb.TryGetPackageInfo("com.example.app", serial);
// pkg.ApkPath, VersionName, VersionCode, IsInstalled

adb.StartApp("com.example.app", serial);   // monkey launcher intent
adb.ForceStop("com.example.app", serial);
adb.Uninstall("com.example.app", serial);
```

When more than one ready device is attached, omit no target: pass an explicit
serial or set `ANDROID_SERIAL`. The library returns an
`AndroidFailureKind.AmbiguousDevice` failure instead of selecting the first
phone.

## Installing an APK

Prefer `AndroidAppInstaller` over raw `Install` when you want validation and post-checks.

```csharp
var installer = new AndroidAppInstaller(adb);

var check = installer.ValidateApk(@"D:\out\app.apk");
if (!check.Ok)
    throw new InvalidOperationException(string.Join("; ", check.Errors));

var result = installer.Install(@"D:\out\app.apk", new ApkInstallOptions
{
    Serial = null,                         // or explicit serial / ANDROID_SERIAL
    Reinstall = true,                      // -r
    GrantPermissions = true,               // -g
    AllowDowngrade = false,                // -d
    ExpectedPackageName = "com.example.app",
    VerifyInstalled = true,                // pm path after install
    LaunchAfterInstall = false,
    DeviceWaitTimeout = TimeSpan.FromSeconds(45),
});

Console.WriteLine(result.Message);
if (result.Package is { } p)
    Console.WriteLine($"{p.PackageName} {p.VersionName} ({p.VersionCode})");
```

### What validation covers

| Check | Default |
|-------|---------|
| Path exists / resolvable | required |
| Minimum size | 1024 bytes (`MinApkBytes`) |
| Optional max size | `MaxApkBytes` |
| Readable zip with `AndroidManifest.xml` | `RequireApkEntries = true` |
| `classes*.dex` | warning if missing |

`VerifyInstalled` only runs when `ExpectedPackageName` is set. The installer
accepts one `.apk`; `.apks` and `.xapk` are reported as unsupported bundle
artifacts rather than being passed to the single-APK path.

### Low-level install

```csharp
adb.Install(apkPath, reinstall: true, serial);
adb.Install(apkPath, serial, "-r", "-g"); // explicit flags
```

## Diagnostics and dogfooding

The reusable diagnostics surface can capture evidence without a UI:

```powershell
dotnet run --project d:\novolis\novolis-utilities\src\Adb\Adb.csproj -p:NovolisUseProjectReferences=true
dotnet run --project d:\novolis\novolis-utilities\src\Adb\Adb.csproj -p:NovolisUseProjectReferences=true -- --smoke --serial R58M12ABCDE
```

For a repeatable developer command, install the PackAsTool
`Novolis.Tools.Android.Cli` and use `novolis-android doctor`,
`novolis-android app install`, `novolis-android logcat`, `novolis-android
screen shot`, and `novolis-android diagnostics collect`.

## Non-goals

- Android SDK / emulator / AVD lifecycle management  
- scrcpy or a full remote-screen viewer  
- On-device Xamarin / MAUI bindings  
- Full Appium test orchestration  

## Related

| Package / app | Role |
|---------------|------|
| [AdvancedSharpAdbClient](https://www.nuget.org/packages/AdvancedSharpAdbClient/) | ADB protocol implementation |
| `Adb` (dogfooding) | Avalonia UI + `--smoke` against a tethered phone |
| `novolis-android` | Scriptable install, lifecycle, evidence, and diagnostics workflow |
| `Novolis.IO.Git` | Same “thin driver” pattern for local `git` |
| `Novolis.IO.GitHub` | OAuth + sparse mirror used by Books Mobile |
| `IoSmoke` (dogfooding) | Paths / Recovery / Watching / Processes / Git |

