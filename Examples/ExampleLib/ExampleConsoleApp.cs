using System;
using System.Threading;
using System.Threading.Tasks;
using Tapeti;

namespace ExampleLib
{
    /// <summary>
    /// Callback method for ExampleConsoleApp.Run
    /// </summary>
    /// <param name="dependencyResolver">A reference to the dependency resolver passed to the ExampleConsoleApp</param>
    /// <param name="waitForDone">Await this function to wait for the Done signal</param>
    public delegate Task AsyncFunc(IDependencyResolver dependencyResolver, Func<Task> waitForDone);


    /// <summary>
    /// Since the examples do not run as a service, we need to know when the example has run
    /// to completion. This helper injects IExampleState into the container which
    /// can be used to signal that it has finished. It also provides the Wait
    /// method to wait for this signal.
    /// </summary>
    public class ExampleConsoleApp 
    {
        private readonly IDependencyContainer dependencyResolver;
        private readonly int expectedDoneCount;
        private int doneCount = 0;
        private readonly TaskCompletionSource<bool> doneSignal = new TaskCompletionSource<bool>();


        /// <param name="dependencyResolver">Uses Tapeti's IDependencyContainer interface so you can easily switch an example to your favourite IoC container</param>
        public ExampleConsoleApp(IDependencyContainer dependencyResolver, int expectedDoneCount = 1)
        {
            this.dependencyResolver = dependencyResolver;
            this.expectedDoneCount = expectedDoneCount;
            
            dependencyResolver.RegisterDefault<IExampleState>(() => new ExampleState(this));
        }


        /// <summary>
        /// Runs the specified async method and waits for completion. Handles exceptions and waiting
        /// for user input when the example application finishes.
        /// </summary>
        /// <param name="asyncFunc"></param>
        public void Run(AsyncFunc asyncFunc)
        {
            try
            {
                asyncFunc(dependencyResolver, WaitAsync).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(UnwrapException(e));
            }
            finally
            {
                Console.WriteLine("Press any Enter key to continue...");
                Console.ReadLine();
            }
        }


        /// <summary>
        /// Returns a Task which completed when IExampleState.Done is called
        /// </summary>
        public async Task WaitAsync()
        {
            await doneSignal.Task;
            
            // This is a hack, because the signal is often given in a message handler before the message can be
            // acknowledged, causing it to be put back on the queue because the connection is closed.
            // This short delay allows consumers to finish. This is not an issue in a proper service application.
            await Task.Delay(500);
        }


        internal Exception UnwrapException(Exception e)
        {
            while (true)
            {
                if (!(e is AggregateException aggregateException)) 
                    return e;

                if (aggregateException.InnerExceptions.Count != 1)
                    return e;

                e = aggregateException.InnerExceptions[0];
            }
        }

        internal void Done()
        {
            if (Interlocked.Increment(ref doneCount) == expectedDoneCount)
                doneSignal.TrySetResult(true);
        }


        private class ExampleState : IExampleState
        {
            private readonly ExampleConsoleApp owner;


            public ExampleState(ExampleConsoleApp owner)
            {
                this.owner = owner;
            }


            public void Done()
            {
                owner.Done();
            }
        }
    }
}
