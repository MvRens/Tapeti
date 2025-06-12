using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Tapeti.Tasks;
using Tapeti.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client;

public class SerialTaskQueueTests
{
    private readonly ITestOutputHelper testOutputHelper;


    public SerialTaskQueueTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }


    [Fact]
    public async Task Enqueue()
    {
        await using var queue = new SerialTaskQueue();
        const int taskCount = 3;

        var taskStarted = Enumerable.Range(0, taskCount).Select(_ => new TaskCompletionSource()).ToArray();
        var taskResume = Enumerable.Range(0, taskCount).Select(_ => new TaskCompletionSource()).ToArray();
        var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.CompletedTask).ToArray();


        // ReSharper disable MoveLocalFunctionAfterJumpStatement - I don't like that style as it obscures the intended scope
        void StartTask(int index)
        {
            tasks[index] = queue.Add(async () =>
            {
                testOutputHelper.WriteLine($"Task {index} started");
                taskStarted[index].SetResult();
                await taskResume[index].Task;
                testOutputHelper.WriteLine($"Task {index} resumed");
            }).AsTask();
        }

        void ResumeTask(int index)
        {
            taskResume[index].SetResult();
        }


        StartTask(0);
        StartTask(1);
        StartTask(2);

        await taskStarted[0].Task.WithTimeout(TimeSpan.FromSeconds(1), "taskStarted[0]");
        taskStarted[1].Task.IsCompleted.ShouldBeFalse("Task 0 started but not resumed, task 1 should not have started yet");
        taskStarted[2].Task.IsCompleted.ShouldBeFalse("Task 0 started but not resumed, task 2 should not have started yet");

        ResumeTask(0);
        await tasks[0].WithTimeout(TimeSpan.FromSeconds(1), "tasks[0]");

        await taskStarted[1].Task.WithTimeout(TimeSpan.FromSeconds(1), "taskStarted[1]");
        taskStarted[2].Task.IsCompleted.ShouldBeFalse("Task 1 started but not resumed, task 2 should not have started yet");

        ResumeTask(1);
        await tasks[1].WithTimeout(TimeSpan.FromSeconds(1), "tasks[1]");

        await taskStarted[2].Task.WithTimeout(TimeSpan.FromSeconds(1), "taskStarted[2]");
        ResumeTask(2);
        await tasks[2].WithTimeout(TimeSpan.FromSeconds(1), "tasks[2]");;
    }
}
