using Microsoft.Extensions.FileProviders;
using Novolis.IO.Processes;
using Novolis.IO.Workspace;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class ProcessTreeTests
{
    [Test]
    public async Task Kill_noops_for_invalid_pid()
    {
        ProcessTree.Kill(0);
        ProcessTree.Kill(-1);
        var queue = new ProcessJobQueue();
        await Assert.That(queue.MaxParallel).IsEqualTo(2);
    }
}
