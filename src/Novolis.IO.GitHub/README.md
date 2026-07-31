# Novolis.IO.GitHub

GitHub **OAuth device flow** and a sparse **`content/` repository mirror** (Pull + Save/Commit/Push) via Octokit Git Data API. Aimed at Books Mobile / Studio hosts that should not require a full local `git` clone on every device.

## Install

```bash
dotnet add package Novolis.IO.GitHub
```

Depends on **Octokit**. Needs a GitHub OAuth App **client id** with device flow enabled and appropriate scopes (typically `repo` for private content).

## Quick start — device auth

```csharp
using Novolis.IO.GitHub;

var auth = new GitHubDeviceAuth();
var device = await auth.RequestDeviceCodeAsync(clientId, scope: "repo");
// Show device.UserCode; prefer opening device.VerificationUriComplete
var token = await auth.WaitForAccessTokenAsync(clientId, device);
```

Manual poll loop: `PollForTokenAsync` returns `DeviceTokenResult` (`Success`, `IsPending`, errors).

## Quick start — content mirror

```csharp
using Novolis.IO.GitHub;
using Octokit;

var client = BooksRepoMirror.CreateClient(accessToken);
var mirror = new BooksRepoMirror(client, new BooksRepoMirrorOptions
{
    Owner = "frankhaugen",
    Name = "books",
    WorkspaceRoot = workspaceDir,
    ContentPrefix = "content/", // default
});

var pull = await mirror.PullAsync();
mirror.NoteDirty("content/series/.../chapters/001.md");
var push = await mirror.SaveCommitPushAsync(); // message: BooksMobile yyyy-MM-dd HH:mm
```

Mirror state is kept under `{WorkspaceRoot}/.novolis/mobile-mirror.json`.

## API

| Type | Role |
|------|------|
| `GitHubDeviceAuth` | Device code + token wait/poll |
| `DeviceCodeResponse` | User code, verification URIs, interval, expiry |
| `DeviceTokenResult` | Access token or pending/error |
| `BooksRepoMirror` | Sparse pull / dirty tracking / commit+push |
| `BooksRepoMirrorOptions` | Owner, name, workspace root, content prefix |
| `MirrorPullResult` / `MirrorPushResult` | Ok, message, commit SHA, file count |

## Dogfooding / apps

Used by Books Mobile deploy and Studio flows. Local IO smoke does not cover GitHub (needs client id + network).

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Git` | Local `git` process helper for desktop Studio |
| `Novolis.IO.Mobile.Android` | Install / debug Android builds that use this mirror |
