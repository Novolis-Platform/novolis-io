using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.Git;

sealed class PassesFile
{
    public string? ActivePass { get; set; }
    public List<PassEntry> Passes { get; set; } = [];
}
