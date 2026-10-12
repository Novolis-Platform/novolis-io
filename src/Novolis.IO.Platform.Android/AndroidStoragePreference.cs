namespace Novolis.IO.Platform.Android;

/// <summary>Controls whether an Android product uses shared or app-private storage.</summary>
public enum AndroidStoragePreference
{
    /// <summary>Prefer the user-visible Documents/Novolis product tree.</summary>
    SharedDocuments,

    /// <summary>Use app-specific external files, falling back to internal files.</summary>
    AppPrivate,
}
