using System.Net;
using System.Text;
using System.Text.Json;
using Novolis.IO.GitHub;
using Octokit;
using Octokit.Internal;

namespace Novolis.IO.Unit;

public sealed class BooksRepoMirrorOctokitTests
{
    const string CommitSha = "abc1234567890abcdef1234567890abcdef1234567";
    const string TreeSha = "tree1234567890abcdef1234567890abcdef1234567";
    const string BlobSha = "blob1111111111111111111111111111111111111111";

    [Test]
    public async Task PullAsync_downloads_text_blobs_and_skips_assets()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-pull-");
        try
        {
            var api = new FakeGitHubApi();
            api.AddBlob(BlobSha, "# Hello", "utf-8");
            api.SetTree([
                new FakeTreeEntry("content/readme.md", BlobSha),
                new FakeTreeEntry("content/assets/logo.png", "blob2222222222222222222222222222222222222222"),
                new FakeTreeEntry("content/chapters/one.bin", "blob3333333333333333333333333333333333333333"),
            ]);

            var mirror = CreateMirror(temp.FullName, api);
            var result = await mirror.PullAsync();

            await Assert.That(result.Ok).IsTrue().Because(result.Message);
            await Assert.That(result.FileCount).IsEqualTo(1);
            await Assert.That(result.CommitSha).IsEqualTo(CommitSha);
            await Assert.That(mirror.Branch).IsEqualTo("main");

            var local = Path.Combine(temp.FullName, "content", "readme.md");
            await Assert.That(File.Exists(local)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(local)).IsEqualTo("# Hello");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task PullAsync_returns_failure_when_repo_missing()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-fail-");
        try
        {
            var api = new FakeGitHubApi { FailRepository = true };
            var mirror = CreateMirror(temp.FullName, api);
            var result = await mirror.PullAsync();
            await Assert.That(result.Ok).IsFalse();
            await Assert.That(result.Message).IsNotNull();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task SaveCommitPushAsync_pushes_dirty_files_after_pull()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-push-");
        try
        {
            var api = new FakeGitHubApi();
            api.AddBlob(BlobSha, "old", "utf-8");
            api.SetTree([new FakeTreeEntry("content/readme.md", BlobSha)]);

            var mirror = CreateMirror(temp.FullName, api);
            var pull = await mirror.PullAsync();
            await Assert.That(pull.Ok).IsTrue().Because(pull.Message);

            var local = Path.Combine(temp.FullName, "content", "readme.md");
            await File.WriteAllTextAsync(local, "updated body");
            mirror.NoteDirty("content/readme.md");

            var push = await mirror.SaveCommitPushAsync("test commit");
            await Assert.That(push.Ok).IsTrue().Because(push.Message);
            await Assert.That(push.FileCount).IsEqualTo(1);
            await Assert.That(push.CommitSha).IsNotNull();
            await Assert.That(mirror.DirtyCount).IsEqualTo(0);
            await Assert.That(api.ReferenceUpdates).IsGreaterThan(0);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task SaveCommitPushAsync_with_no_dirty_files_is_noop()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-noop-");
        try
        {
            var api = new FakeGitHubApi();
            api.AddBlob(BlobSha, "x", "utf-8");
            api.SetTree([new FakeTreeEntry("content/readme.md", BlobSha)]);

            var mirror = CreateMirror(temp.FullName, api);
            await mirror.PullAsync();
            var push = await mirror.SaveCommitPushAsync();
            await Assert.That(push.Ok).IsTrue().Because(push.Message);
            await Assert.That(push.Message).Contains("Nothing to commit");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task SaveCommitPushAsync_skips_missing_local_files()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-missing-");
        try
        {
            var api = new FakeGitHubApi();
            api.AddBlob(BlobSha, "x", "utf-8");
            api.SetTree([new FakeTreeEntry("content/readme.md", BlobSha)]);

            var mirror = CreateMirror(temp.FullName, api);
            await mirror.PullAsync();
            mirror.NoteDirty("content/ghost.md");
            var push = await mirror.SaveCommitPushAsync();
            await Assert.That(push.Ok).IsTrue().Because(push.Message);
            await Assert.That(push.Message).Contains("Nothing to commit");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task PullAsync_decodes_base64_blobs()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-b64-");
        try
        {
            var api = new FakeGitHubApi();
            api.AddBlob(BlobSha, Convert.ToBase64String("# B64"u8.ToArray()), "base64");
            api.SetTree([new FakeTreeEntry("content/base64.md", BlobSha)]);

            var mirror = CreateMirror(temp.FullName, api);
            var result = await mirror.PullAsync();
            await Assert.That(result.Ok).IsTrue().Because(result.Message);
            var text = await File.ReadAllTextAsync(Path.Combine(temp.FullName, "content", "base64.md"));
            await Assert.That(text).IsEqualTo("# B64");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    static BooksRepoMirror CreateMirror(string workspaceRoot, FakeGitHubApi api)
    {
        var connection = new Connection(
            new ProductHeaderValue("Novolis.IO.Test"),
            new Uri("https://api.github.com"),
            new InMemoryCredentialStore(new Credentials("test-token")),
            new HttpClientAdapter(() => new FakeGitHubHandler(api)),
            new SimpleJsonSerializer());
        return new BooksRepoMirror(new GitHubClient(connection), new BooksRepoMirrorOptions
        {
            Owner = "test-owner",
            Name = "books",
            WorkspaceRoot = workspaceRoot,
        });
    }

    sealed record FakeTreeEntry(string Path, string Sha);

    sealed class FakeGitHubApi
    {
        readonly Dictionary<string, (string Content, string Encoding)> _blobs = new(StringComparer.Ordinal);
        readonly List<string> _tree = [];
        int _blobCounter = 100;

        public bool FailRepository { get; set; }
        public int ReferenceUpdates { get; private set; }

        public void AddBlob(string sha, string content, string encoding) =>
            _blobs[sha] = (content, encoding);

        public void SetTree(IEnumerable<FakeTreeEntry> entries)
        {
            _tree.Clear();
            foreach (var e in entries)
                _tree.Add(JsonSerializer.Serialize(new { path = e.Path, mode = "100644", type = "blob", sha = e.Sha, size = 1 }));
        }

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (FailRepository && path.Contains("/repos/", StringComparison.Ordinal))
                return Json(HttpStatusCode.NotFound, """{"message":"Not Found"}""");

            if (request.Method == HttpMethod.Get && path.Contains("/repos/test-owner/books", StringComparison.Ordinal) && !path.Contains("/git/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, """{"id":1,"name":"books","full_name":"test-owner/books","default_branch":"main","owner":{"login":"test-owner","id":1}}""");

            if (request.Method == HttpMethod.Get && path.Contains("/git/refs/heads/main", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, "{\"ref\":\"refs/heads/main\",\"object\":{\"type\":\"commit\",\"sha\":\"" + CommitSha + "\"}}");

            if (request.Method == HttpMethod.Get && path.Contains("/git/commits/" + CommitSha, StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, "{\"sha\":\"" + CommitSha + "\",\"tree\":{\"sha\":\"" + TreeSha + "\"}}");

            if (request.Method == HttpMethod.Get && path.Contains("/git/trees/" + TreeSha, StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, "{\"sha\":\"" + TreeSha + "\",\"truncated\":false,\"tree\":[" + string.Join(',', _tree) + "]}");

            if (request.Method == HttpMethod.Get && path.Contains("/git/blobs/", StringComparison.Ordinal))
            {
                var sha = path.Split('/').Last();
                if (_blobs.TryGetValue(sha, out var blob))
                {
                    var body = "{\"sha\":\"" + sha + "\",\"content\":" + JsonSerializer.Serialize(blob.Content) + ",\"encoding\":\"" + blob.Encoding + "\"}";
                    return Json(HttpStatusCode.OK, body);
                }
                return Json(HttpStatusCode.NotFound, """{"message":"missing"}""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/git/blobs", StringComparison.Ordinal))
            {
                _blobCounter++;
                var sha = ("blob" + _blobCounter.ToString("D40"))[..40];
                _blobs[sha] = ("stored", "utf-8");
                return Json(HttpStatusCode.Created, "{\"sha\":\"" + sha + "\"}");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/git/trees", StringComparison.Ordinal))
            {
                var sha = "tree9999999999999999999999999999999999999999";
                return Json(HttpStatusCode.Created, "{\"sha\":\"" + sha + "\"}");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/git/commits", StringComparison.Ordinal))
            {
                var sha = "commit999999999999999999999999999999999999999";
                return Json(HttpStatusCode.Created, "{\"sha\":\"" + sha + "\"}");
            }

            if (request.Method == HttpMethod.Patch && path.Contains("/git/refs/heads/main", StringComparison.Ordinal))
            {
                ReferenceUpdates++;
                return Json(HttpStatusCode.OK, """{"ref":"refs/heads/main"}""");
            }

            return Json(HttpStatusCode.NotFound, "{\"message\":\"unhandled " + path + " " + request.Method + "\"}");
        }

        static HttpResponseMessage Json(HttpStatusCode code, string body) =>
            new(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    sealed class FakeGitHubHandler(FakeGitHubApi api) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(api.Respond(request));
    }
}
