using System.Net;
using System.Text;
using Novolis.IO.GitHub;
using Novolis.IO.Processes;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class IoCoverageGapTests
{
    [Test]
    public async Task DeviceAuth_RequestFailure_ThrowsWithStatus()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad client", Encoding.UTF8, "text/plain"),
            });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        await Assert.That(async () => await auth.RequestDeviceCodeAsync("client"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeviceAuth_IncompleteResponse_Throws()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"device_code":"only"}""", Encoding.UTF8, "application/json"),
            });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        await Assert.That(async () => await auth.RequestDeviceCodeAsync("client"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeviceAuth_GitHubAppClient_OmitsScope()
    {
        string? capturedBody = null;
        var handler = new CaptureHandler(async req =>
        {
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"device_code":"d","user_code":"U","verification_uri":"https://github.com/login/device","interval":5,"expires_in":900}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        _ = await auth.RequestDeviceCodeAsync("Iv1.test-app");
        await Assert.That(capturedBody).IsNotNull();
        await Assert.That(capturedBody!).Contains("client_id=Iv1.test-app");
        await Assert.That(capturedBody!).DoesNotContain("scope=");
    }

    [Test]
    public async Task DeviceAuth_WaitForAccessToken_FailsOnNonPendingError()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error":"access_denied","error_description":"User denied"}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        var device = new DeviceCodeResponse
        {
            DeviceCode = "dev",
            UserCode = "ABCD",
            VerificationUri = new Uri("https://github.com/login/device"),
            Interval = TimeSpan.FromMilliseconds(1),
            ExpiresInSeconds = 5,
        };
        await Assert.That(async () => await auth.WaitForAccessTokenAsync("client", device))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeviceAuth_WaitForAccessToken_TimesOut()
    {
        var handler = new RepeatingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error":"authorization_pending"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        var device = new DeviceCodeResponse
        {
            DeviceCode = "dev",
            UserCode = "ABCD",
            VerificationUri = new Uri("https://github.com/login/device"),
            Interval = TimeSpan.FromMilliseconds(1),
            ExpiresInSeconds = 1,
        };
        await Assert.That(async () => await auth.WaitForAccessTokenAsync("client", device))
            .Throws<TimeoutException>();
    }

    [Test]
    public async Task MirrorPullResult_StoresFields()
    {
        var result = new MirrorPullResult(true, "ok", "sha123", 3);
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.CommitSha).IsEqualTo("sha123");
        await Assert.That(result.FileCount).IsEqualTo(3);
    }

    [Test]
    public async Task InMemoryFileWorkspace_WriteBytesTextDeleteAndMove()
    {
        var ws = new InMemoryFileWorkspace(@"C:\gap");
        ws.WriteAllBytes(@"C:\gap\bin.dat", [1, 2, 3]);
        await ws.WriteAllTextAsync(@"C:\gap\readme.md", "# hi");
        await Assert.That(ws.FileExists(@"C:\gap\bin.dat")).IsTrue();
        await Assert.That(await ws.ReadAllTextAsync(@"C:\gap\readme.md")).IsEqualTo("# hi");

        ws.DeleteFile(@"C:\gap\bin.dat");
        await Assert.That(ws.FileExists(@"C:\gap\bin.dat")).IsFalse();

        ws.WriteAllText(@"C:\gap\src.txt", "src");
        ws.MoveFile(@"C:\gap\src.txt", @"C:\gap\dst.txt", overwrite: true);
        await Assert.That(ws.FileExists(@"C:\gap\dst.txt")).IsTrue();
    }

    [Test]
    public async Task ProcessJobQueue_ChangedEventFires()
    {
        var queue = new ProcessJobQueue { MaxParallel = 1 };
        var hits = 0;
        queue.Changed += () => hits++;
        var job = queue.Enqueue(EchoSpec(0));
        await WaitForStatus(job, ProcessJobStatus.Succeeded, TimeSpan.FromSeconds(10));
        await Assert.That(hits).IsGreaterThan(0);
    }

    static ProcessJobSpec EchoSpec(int exitCode) =>
        OperatingSystem.IsWindows()
            ? new ProcessJobSpec { FileName = "cmd.exe", Arguments = ["/c", $"exit /b {exitCode}"], Title = "echo" }
            : new ProcessJobSpec { FileName = "/bin/sh", Arguments = ["-c", $"exit {exitCode}"], Title = "echo" };

    static async Task WaitForStatus(ProcessJob job, ProcessJobStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (job.Status == status)
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException($"Job {job.Id} stayed {job.Status}; expected {status}. Detail: {job.Detail}");
    }

    sealed class QueueHandler : HttpMessageHandler
    {
        readonly Queue<HttpResponseMessage> _responses;

        public QueueHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responses.Dequeue());
    }

    sealed class RepeatingHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(factory());
    }

    sealed class CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            respond(request);
    }
}
