# Novolis.IO.GitHub

GitHub OAuth **device flow** and a sparse `content/` repository mirror (`Pull` + `Save/Commit/Push`) via Octokit Git Data API.

## Install

```bash
dotnet add package Novolis.IO.GitHub
```

## Quick start

```csharp
using Novolis.IO.GitHub;
using Octokit;

var auth = new GitHubDeviceAuth();
var device = await auth.RequestDeviceCodeAsync(clientId, scope: "repo");
// Show device.UserCode; open device.VerificationUri (GitHub app / passkey)
var token = await auth.WaitForAccessTokenAsync(clientId, device);

var client = BooksRepoMirror.CreateClient(token);
var mirror = new BooksRepoMirror(client, new BooksRepoMirrorOptions
{
    Owner = "frankhaugen",
    Name = "books",
    WorkspaceRoot = workspaceDir,
});
await mirror.PullAsync();
mirror.NoteDirty("content/series/.../chapters/001.md");
await mirror.SaveCommitPushAsync(); // auto message BooksMobile yyyy-MM-dd HH:mm
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Git` | Process `git` for desktop Studio (not used on Android) |
