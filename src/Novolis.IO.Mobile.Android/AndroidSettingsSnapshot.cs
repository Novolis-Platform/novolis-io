namespace Novolis.IO.Mobile.Android;

/// <summary>Selected Android settings relevant to provisioning and privacy.</summary>
public sealed record AndroidSettingsSnapshot
{
    /// <summary>
    /// Values keyed as <c>global.&lt;name&gt;</c> or <c>secure.&lt;name&gt;</c>.
    /// A missing Android setting is represented by a null value.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
