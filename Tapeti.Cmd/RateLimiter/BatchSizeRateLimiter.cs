using System;
using System.Threading;
using Tapeti.Cmd.ConsoleHelper;

namespace Tapeti.Cmd.RateLimiter
{
    public abstract class BaseBatchSizeRateLimiter : IRateLimiter
    {
        private readonly IConsole console;
        private readonly IRateLimiter decoratedRateLimiter;
        private readonly int batchSize;
        private int batchCount;


        protected BaseBatchSizeRateLimiter(IConsole console, IRateLimiter decoratedRateLimiter, int batchSize)
        {
            this.console = console;
            this.decoratedRateLimiter = decoratedRateLimiter;
            this.batchSize = batchSize;
        }


        public void Execute(Action action)
        {
            batchCount++;
            if (batchCount > batchSize)
            {
                Pause(console);
                batchCount = 1;
            }

            decoratedRateLimiter.Execute(action);
        }


        protected abstract void Pause(IConsole console);
    }


    public class ManualBatchSizeRateLimiter : BaseBatchSizeRateLimiter
    {
        public ManualBatchSizeRateLimiter(IConsole console, IRateLimiter decoratedRateLimiter, int batchSize) : base(console, decoratedRateLimiter, batchSize)
        {
        }

        
        protected override void Pause(IConsole console)
        {
            using var consoleWriter = console.GetTemporaryWriter();
            consoleWriter.Confirm("Press any key to continue with the next batch...");
        }
    }

    
    public class TimedBatchSizeRateLimiter : BaseBatchSizeRateLimiter
    {
        private readonly int batchPauseTime;
        

        public TimedBatchSizeRateLimiter(IConsole console, IRateLimiter decoratedRateLimiter, int batchSize, int batchPauseTime) : base(console, decoratedRateLimiter, batchSize)
        {
            this.batchPauseTime = batchPauseTime;
        }

        
        protected override void Pause(IConsole console)
        {
            using var consoleWriter = console.GetTemporaryWriter();
            
            var remaining = batchPauseTime;
            while (remaining > 0 && !console.Cancelled)
            {
                consoleWriter.WriteLine($"Next batch in {remaining} second{(remaining != 1 ? "s" : "")}...");
                
                Thread.Sleep(1000);
                remaining--;
            }
        }
    }
}
