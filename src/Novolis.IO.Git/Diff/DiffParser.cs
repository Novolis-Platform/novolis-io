namespace Novolis.IO.Git;

/// <summary>Parses unified git diffs into <see cref="DiffDocument"/>.</summary>
public static class DiffParser
{
    /// <summary>Parses unified diff text.</summary>
    public static DiffDocument Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new DiffDocument();

        var files = new List<DiffFile>();
        string? path = null;
        string? oldPath = null;
        var isBinary = false;
        var fileHunks = new List<DiffHunk>();
        string? hunkHeader = null;
        var lines = new List<DiffLine>();

        void CommitHunk()
        {
            if (hunkHeader is null)
                return;
            fileHunks.Add(new DiffHunk { Header = hunkHeader, Lines = lines.ToArray() });
            hunkHeader = null;
            lines.Clear();
        }

        void CommitFile()
        {
            if (path is null)
                return;
            CommitHunk();
            files.Add(new DiffFile
            {
                Path = path,
                OldPath = oldPath,
                IsBinary = isBinary,
                Hunks = fileHunks.ToArray(),
            });
            path = null;
            oldPath = null;
            isBinary = false;
            fileHunks.Clear();
        }

        foreach (var rawLine in text.Split(['\r', '\n']))
        {
            if (rawLine.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                CommitFile();
                var bits = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (bits.Length >= 4)
                {
                    oldPath = bits[2].StartsWith("a/", StringComparison.Ordinal) ? bits[2][2..] : bits[2];
                    path = bits[3].StartsWith("b/", StringComparison.Ordinal) ? bits[3][2..] : bits[3];
                }

                continue;
            }

            if (rawLine.StartsWith("Binary files ", StringComparison.Ordinal)
                || rawLine.Contains("GIT binary patch", StringComparison.Ordinal))
            {
                isBinary = true;
                continue;
            }

            if (rawLine.StartsWith("+++ ", StringComparison.Ordinal))
            {
                var p = rawLine[4..].Trim();
                if (p.StartsWith("b/", StringComparison.Ordinal))
                    path = p[2..];
                else if (p != "/dev/null")
                    path = p;
                continue;
            }

            if (rawLine.StartsWith("--- ", StringComparison.Ordinal))
            {
                var p = rawLine[4..].Trim();
                if (p.StartsWith("a/", StringComparison.Ordinal))
                    oldPath = p[2..];
                else if (p != "/dev/null")
                    oldPath = p;
                continue;
            }

            if (rawLine.StartsWith("@@", StringComparison.Ordinal))
            {
                CommitHunk();
                hunkHeader = rawLine;
                continue;
            }

            if (hunkHeader is null)
                continue;

            if (rawLine.Length == 0)
            {
                lines.Add(new DiffLine { Kind = ' ', Text = "" });
                continue;
            }

            var kind = rawLine[0];
            if (kind is '+' or '-' or ' ')
                lines.Add(new DiffLine { Kind = kind, Text = rawLine[1..] });
        }

        CommitFile();
        return new DiffDocument { Files = files };
    }
}
