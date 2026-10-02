namespace Novolis.IO.Ndjson;

/// <summary>Pure navigation transitions for bounded record slices.</summary>
public static class ViewerNavigation
{
    /// <summary>Moves one page toward the beginning.</summary>
    public static ViewerState Previous(ViewerState state) =>
        state with
        {
            Skip = Math.Max(0, state.Skip - state.Take),
            Error = null,
        };

    /// <summary>Moves one page toward the end when more records exist.</summary>
    public static ViewerState Next(ViewerState state) =>
        state.Slice is not { HasMore: true }
            ? state
            : state with
            {
                Skip = checked(state.Skip + state.Take),
                Error = null,
            };

    /// <summary>Starts a read at a physical record number.</summary>
    public static ViewerState Jump(ViewerState state, long skip) =>
        state with { Skip = skip, Error = null };

    /// <summary>Changes the page size while retaining the logical location.</summary>
    public static ViewerState ChangeTake(ViewerState state, int take) =>
        state with { Take = take, Error = null };

    /// <summary>Marks a refresh operation as started.</summary>
    public static ViewerState BeginRefresh(ViewerState state) =>
        state with { IsRefreshing = true, Error = null };

    /// <summary>Marks a refresh operation as complete.</summary>
    public static ViewerState CompleteRefresh(ViewerState state) =>
        state with { IsRefreshing = false };

    /// <summary>Captures a failed operation.</summary>
    public static ViewerState Fail(ViewerState state, Exception error) =>
        state with { IsRefreshing = false, Error = error };
}
