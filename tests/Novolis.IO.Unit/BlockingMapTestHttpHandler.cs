namespace Novolis.IO.Unit;

internal sealed class BlockingMapTestHttpHandler : HttpMessageHandler
{
    public TaskCompletionSource<bool> Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<HttpResponseMessage> Response { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> Cancelled { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> Completed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult(true);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            Cancelled);
        try
        {
            return await Response.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Completed.TrySetResult(true);
        }
    }
}
