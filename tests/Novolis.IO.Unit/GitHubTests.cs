using System.Net;
using System.Text;
using Novolis.IO.GitHub;

namespace Novolis.IO.Unit;

public sealed class GitHubTests
{
    [Test]
    public async Task DeviceAuth_RequestDeviceCode_ParsesJson()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"device_code":"dev","user_code":"ABCD-1234","verification_uri":"https://github.com/login/device","interval":5,"expires_in":900}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        var device = await auth.RequestDeviceCodeAsync("client");
        await Assert.That(device.DeviceCode).IsEqualTo("dev");
        await Assert.That(device.UserCode).IsEqualTo("ABCD-1234");
        await Assert.That(device.VerificationUri.AbsoluteUri).IsEqualTo("https://github.com/login/device");
        await Assert.That(device.VerificationUriComplete.Query).Contains("user_code=ABCD-1234");
        await Assert.That(device.Interval.TotalSeconds).IsEqualTo(5);
    }

    [Test]
    public async Task DeviceAuth_Poll_PendingThenSuccess()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error":"authorization_pending","error_description":"waiting"}""",
                    Encoding.UTF8,
                    "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"gho_test","token_type":"bearer","scope":"repo"}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var auth = new GitHubDeviceAuth(new HttpClient(handler));
        var pending = await auth.PollForTokenAsync("client", "dev");
        await Assert.That(pending.IsPending).IsTrue();
        var ok = await auth.PollForTokenAsync("client", "dev");
        await Assert.That(ok.Success).IsTrue();
        await Assert.That(ok.AccessToken).IsEqualTo("gho_test");
    }

    [Test]
    public async Task Mirror_NoteDirty_PersistsAndClearsOnEmptyPush()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-github-");
        try
        {
            var client = SparseRepoMirror.CreateClient("gho_dummy_for_state_only");
            var mirror = new SparseRepoMirror(client, new SparseRepoMirrorOptions
            {
                Owner = "frankhaugen",
                Name = "books",
                WorkspaceRoot = temp.FullName,
            });

            mirror.NoteDirty("content/series/demo/chapters/001.md");
            await Assert.That(mirror.DirtyCount).IsEqualTo(1);

            var stateDir = Path.Combine(temp.FullName, ".novolis");
            Directory.CreateDirectory(stateDir);
            await File.WriteAllTextAsync(
                Path.Combine(stateDir, "mobile-mirror.json"),
                """{"branch":"main","commitSha":"abc1234deadbeef","files":{},"dirty":["content/series/demo/chapters/001.md"]}""");

            var result = await mirror.SaveCommitPushAsync();
            await Assert.That(result.Ok).IsTrue();
            await Assert.That(result.FileCount).IsEqualTo(0);
            await Assert.That(mirror.DirtyCount).IsEqualTo(0);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task DeviceAuth_WaitForAccessToken_returns_token()
    {
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error":"authorization_pending"}""",
                    Encoding.UTF8,
                    "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"gho_wait","token_type":"bearer"}""",
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
            ExpiresInSeconds = 30,
        };
        var token = await auth.WaitForAccessTokenAsync("client", device);
        await Assert.That(token).IsEqualTo("gho_wait");
    }

    [Test]
    public async Task DeviceAuth_result_slow_down_is_pending()
    {
        var result = new DeviceTokenResult(false, null, "slow_down", "slow");
        await Assert.That(result.IsPending).IsTrue();
    }

    sealed class QueueHandler : HttpMessageHandler
    {
        readonly Queue<HttpResponseMessage> _responses;

        public QueueHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No queued HTTP responses.");
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
