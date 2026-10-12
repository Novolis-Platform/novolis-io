namespace Novolis.IO.Platform.Android;

/// <summary>Result of importing a legacy shared product tree.</summary>
public sealed record AndroidStorageImportResult(
    int FilesCopied,
    string? ErrorMessage)
{
    /// <summary>Whether the import discovered or attempted any legacy data.</summary>
    public bool Attempted => FilesCopied > 0 || ErrorMessage is not null;
}
