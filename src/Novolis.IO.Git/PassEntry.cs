using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.Git;

sealed class PassEntry
{
    public PassEntry() { }

    public PassEntry(string id, string branch, string startedAt, string? finishedAt)
    {
        Id = id;
        Branch = branch;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
    }

    public string Id { get; set; } = "";
    public string Branch { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string? FinishedAt { get; set; }
}
