using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.Git;

sealed class PassStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static PassesFile Load(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(path))
            return new PassesFile();
        return JsonSerializer.Deserialize<PassesFile>(File.ReadAllText(path), JsonOpts) ?? new PassesFile();
    }

    public static void Save(string repoRoot, string relativePath, PassesFile file)
    {
        var path = Path.Combine(repoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOpts));
    }
}
