namespace Novolis.IO.Platform.Android;

/// <summary>Where <see cref="AndroidAppStorage"/> placed the product root.</summary>
public enum AndroidStorageKind
{
    /// <summary>
    /// <c>Documents/Novolis/{product}</c> on shared storage.
    /// Visible in the phone Files app and to <c>adb pull</c>.
    /// </summary>
    SharedDocuments,

    /// <summary>
    /// App-specific external files: <c>Android/data/&lt;package&gt;/files/Novolis/{product}</c>.
    /// No storage permission. Removed when the app is uninstalled.
    /// Debug builds can list it with <c>adb shell run-as</c>.
    /// </summary>
    AppExternal,

    /// <summary>Internal <c>files/Novolis/{product}</c> when external storage is not mounted.</summary>
    Internal,
}
