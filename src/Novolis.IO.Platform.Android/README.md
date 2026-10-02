<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-io/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-io/) · [Source](https://github.com/Novolis-Platform/novolis-io)
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Platform.Android

On-device Android directories for Novolis apps. This is not the host-side ADB package (`Novolis.IO.Mobile.Android`).

`AndroidAppStorage.Open(product)` creates the default root and a `workspace` folder under it.

## Default location

1. **`Documents/Novolis/{product}`** when the app can create a normal directory there. That folder shows in the phone Files app, and `adb pull` reads it without `run-as`. Creation follows scoped storage: a direct write when the platform allows it, otherwise a MediaStore file in `Documents/Novolis/{product}` so the app owns the tree. No storage permission and no all-files access.
2. **App-specific external files** `Android/data/<package>/files/Novolis/{product}` when shared Documents is not a normal writable directory. This needs no storage permission and is removed on uninstall. Debug builds can list it with `adb shell run-as <package>`.
3. **Internal files** only when external storage is not mounted.

Existing trees under the old internal `files/{product}` directory are moved onto the new root when the new root is still empty.

Logcat on a debug build prints the resolved path and the matching adb command.

## Install

```bash
dotnet add package Novolis.IO.Platform.Android
```

Requires `net10.0-android`.

## Quick start

```csharp
using Novolis.IO.Platform.Android;

var paths = AndroidAppStorage.Open("BooksMobile");
Directory.CreateDirectory(paths.WorkspaceDirectory);
File.WriteAllText(Path.Combine(paths.RootDirectory, "note.txt"), "hello");

// Documents:  adb pull "/storage/emulated/0/Documents/Novolis/BooksMobile"
// App files:  adb shell run-as <applicationId> ls "<paths.RootDirectory>"
Console.WriteLine(AndroidStorageLayout.DescribeForAdb(paths));
```

Avalonia Android hosts pick this up through `AddNovolisMobileAndroid(productName)`.

## API

| Surface | Role |
|---------|------|
| `AndroidAppStorage.Open` | Creates the default root, workspace, and cache directory |
| `AndroidAppStorage.DefaultRoot` | Product root path |
| `AndroidAppLocations.Kind` | `SharedDocuments`, `AppExternal`, or `Internal` |
| `AndroidStorageLayout.DescribeForAdb` | `adb pull` or `adb shell run-as` for the resolved root |
