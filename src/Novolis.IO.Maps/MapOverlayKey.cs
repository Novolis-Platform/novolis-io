namespace Novolis.IO.Maps;

/// <summary>Stable, type-qualified identity for a map overlay.</summary>
public readonly record struct MapOverlayKey
{
    /// <summary>Creates an overlay identity.</summary>
    public MapOverlayKey(MapOverlayKind kind, string id)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown overlay kind.");
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An overlay id is required.", nameof(id));

        Kind = kind;
        Id = id.Trim();
    }

    /// <summary>Overlay family.</summary>
    public MapOverlayKind Kind { get; }

    /// <summary>Host-owned identifier within the overlay family.</summary>
    public string Id { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Id}";
}
