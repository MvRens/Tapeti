using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Tapeti.Default;
using Xunit;

namespace Tapeti.Tests.Default;

public class MessageHandlerTrackerTests
{
    private readonly MessageHandlerTracker tracker = new();
    private readonly Stopwatch stopwatch = new();


    public MessageHandlerTrackerTests()
    {
        stopwatch.Start();
    }


    [Fact]
    public async Task WaitNone()
    {
        await tracker.WaitAll(TimeSpan.FromSeconds(10), CancellationToken.None);

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    }


    [Fact]
    public async Task WaitExitOne()
    {
        tracker.Enter();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            tracker.Exit();
        });

        await tracker.WaitAll(TimeSpan.FromSeconds(10), CancellationToken.None);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
    }


    [Fact]
    public async Task WaitDetachedNoneEntered()
    {
        tracker.Enter();
        var detachedTask = Task.Delay(TimeSpan.FromSeconds(1));
        tracker.Detach(detachedTask);
        tracker.Exit();

        await tracker.WaitAll(TimeSpan.FromSeconds(10), CancellationToken.None);

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
    }


    [Fact]
    public async Task WaitDetachedOneEntered()
    {
        tracker.Enter();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            tracker.Exit();
        });


        tracker.Enter();
        var detachedTask = Task.Delay(TimeSpan.FromSeconds(1));
        tracker.Detach(detachedTask);
        tracker.Exit();

        await tracker.WaitAll(TimeSpan.FromSeconds(10), CancellationToken.None);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
    }
}
