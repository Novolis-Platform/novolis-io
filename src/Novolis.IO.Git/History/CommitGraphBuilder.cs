namespace Novolis.IO.Git;

/// <summary>Options for commit graph layout.</summary>
public sealed class CommitGraphOptions
{
    /// <summary>Max commits.</summary>
    public int MaxCount { get; init; } = 200;

    /// <summary>First-parent only.</summary>
    public bool FirstParent { get; init; }

    /// <summary>Logical row height units.</summary>
    public double RowHeight { get; init; } = 1;

    /// <summary>Logical lane width units.</summary>
    public double LaneWidth { get; init; } = 1;
}

/// <summary>Kind of graph edge.</summary>
public enum CommitEdgeKind
{
    /// <summary>First/parent edge.</summary>
    Parent,

    /// <summary>Additional merge parent.</summary>
    Merge,
}

/// <summary>One commit node with lane geometry.</summary>
public sealed class CommitNode
{
    /// <summary>Full sha.</summary>
    public required string Sha { get; init; }

    /// <summary>Short sha.</summary>
    public required string ShortSha { get; init; }

    /// <summary>Subject.</summary>
    public required string Subject { get; init; }

    /// <summary>Author name.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Author date.</summary>
    public string? AuthorAt { get; init; }

    /// <summary>Parent shas.</summary>
    public IReadOnlyList<string> Parents { get; init; } = [];

    /// <summary>Lane index (0-based).</summary>
    public int Lane { get; init; }

    /// <summary>Row index (0 = newest).</summary>
    public int Row { get; init; }

    /// <summary>Logical X.</summary>
    public double X { get; init; }

    /// <summary>Logical Y.</summary>
    public double Y { get; init; }

    /// <summary>Whether this is a merge (2+ parents).</summary>
    public bool IsMerge { get; init; }
}

/// <summary>Edge between commits.</summary>
public sealed class CommitEdge
{
    /// <summary>Child (newer) sha.</summary>
    public required string From { get; init; }

    /// <summary>Parent (older) sha.</summary>
    public required string To { get; init; }

    /// <summary>Edge kind.</summary>
    public required CommitEdgeKind Kind { get; init; }

    /// <summary>From lane.</summary>
    public int FromLane { get; init; }

    /// <summary>To lane.</summary>
    public int ToLane { get; init; }
}

/// <summary>Lane metadata.</summary>
public sealed class CommitLane
{
    /// <summary>Lane index.</summary>
    public required int Index { get; init; }

    /// <summary>Optional branch name hint.</summary>
    public string? TipName { get; init; }
}

/// <summary>Full graph model for UI / JSON.</summary>
public sealed class CommitGraphModel
{
    /// <summary>Nodes newest-first.</summary>
    public IReadOnlyList<CommitNode> Nodes { get; init; } = [];

    /// <summary>Edges.</summary>
    public IReadOnlyList<CommitEdge> Edges { get; init; } = [];

    /// <summary>Lanes.</summary>
    public IReadOnlyList<CommitLane> Lanes { get; init; } = [];

    /// <summary>Tip refs anchored on nodes.</summary>
    public IReadOnlyList<TipRef> TipRefs { get; init; } = [];
}

/// <summary>Assigns lanes and geometry for a commit list (Avalonia-free).</summary>
public static class CommitGraphBuilder
{
    /// <summary>Builds a graph from commits (newest first) and tip refs.</summary>
    public static CommitGraphModel Build(
        IReadOnlyList<CommitInfo> commits,
        IReadOnlyList<TipRef>? tipRefs = null,
        CommitGraphOptions? options = null)
    {
        options ??= new CommitGraphOptions();
        tipRefs ??= [];
        if (commits.Count == 0)
            return new CommitGraphModel { TipRefs = tipRefs };

        var shaToRow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < commits.Count; i++)
            shaToRow[commits[i].Sha] = i;

        // Active lane occupancy: lane -> sha at that row's "open" tip walking oldest direction
        var laneOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nextLane = 0;
        var edges = new List<CommitEdge>();
        var nodes = new List<CommitNode>(commits.Count);

        for (var row = 0; row < commits.Count; row++)
        {
            var c = commits[row];
            if (!laneOf.TryGetValue(c.Sha, out var lane))
            {
                lane = nextLane++;
                laneOf[c.Sha] = lane;
            }

            var parents = c.Parents;
            if (options.FirstParent && parents.Count > 0)
                parents = [parents[0]];

            for (var pi = 0; pi < parents.Count; pi++)
            {
                var parent = parents[pi];
                int parentLane;
                if (pi == 0)
                {
                    parentLane = lane;
                    laneOf[parent] = parentLane;
                }
                else if (!laneOf.TryGetValue(parent, out parentLane))
                {
                    parentLane = nextLane++;
                    laneOf[parent] = parentLane;
                }

                edges.Add(new CommitEdge
                {
                    From = c.Sha,
                    To = parent,
                    Kind = pi == 0 ? CommitEdgeKind.Parent : CommitEdgeKind.Merge,
                    FromLane = lane,
                    ToLane = parentLane,
                });
            }

            nodes.Add(new CommitNode
            {
                Sha = c.Sha,
                ShortSha = c.ShortSha,
                Subject = c.Subject,
                AuthorName = c.AuthorName,
                AuthorAt = c.AuthorAt,
                Parents = c.Parents,
                Lane = lane,
                Row = row,
                X = lane * options.LaneWidth,
                Y = row * options.RowHeight,
                IsMerge = c.Parents.Count > 1,
            });
        }

        var laneCount = Math.Max(1, nextLane);
        var lanes = Enumerable.Range(0, laneCount)
            .Select(i => new CommitLane
            {
                Index = i,
                TipName = tipRefs.FirstOrDefault(t =>
                    t.Sha is not null
                    && nodes.Any(n => n.Lane == i && n.Row == 0
                        && (n.Sha.StartsWith(t.Sha, StringComparison.OrdinalIgnoreCase)
                            || t.Sha.StartsWith(n.ShortSha, StringComparison.OrdinalIgnoreCase))))?.Name,
            })
            .ToArray();

        // Attach tip names for tips that match any node sha
        var anchoredTips = tipRefs
            .Where(t => t.Sha is not null && nodes.Any(n =>
                n.Sha.StartsWith(t.Sha, StringComparison.OrdinalIgnoreCase)
                || t.Sha.StartsWith(n.ShortSha, StringComparison.OrdinalIgnoreCase)
                || string.Equals(n.Sha, t.Sha, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new CommitGraphModel
        {
            Nodes = nodes,
            Edges = edges,
            Lanes = lanes,
            TipRefs = anchoredTips.Length > 0 ? anchoredTips : tipRefs,
        };
    }
}
