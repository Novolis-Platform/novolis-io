namespace Novolis.IO.Git;

/// <summary>Generic operation result.</summary>
public sealed class GitOperationResult
{
    /// <summary>Creates a result.</summary>
    public GitOperationResult(bool ok, string command, string message, object? data = null)
    {
        Ok = ok;
        Command = command;
        Message = message;
        Data = data;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Logical command name.</summary>
    public string Command { get; }

    /// <summary>Human-readable message.</summary>
    public string Message { get; }

    /// <summary>Optional structured payload.</summary>
    public object? Data { get; }

    /// <summary>Success factory.</summary>
    public static GitOperationResult Success(string command, string message, object? data = null) =>
        new(true, command, message, data);

    /// <summary>Failure factory.</summary>
    public static GitOperationResult Fail(string command, string message, object? data = null) =>
        new(false, command, message, data);
}
