using System.Threading;
using System.Threading.Tasks;
using Tapeti;

namespace Test
{
    public class MarcoEmitter
    {
        private readonly IPublisher publisher;


        public MarcoEmitter(IPublisher publisher)
        {
            this.publisher = publisher;
        }


        public async Task Run()
        {
            //await publisher.Publish(new MarcoMessage());

            /*
            var concurrent = new SemaphoreSlim(20);

            while (true)
            {
                for (var x = 0; x < 200; x++)
                {
                    await concurrent.WaitAsync();
                    try
                    {
                        await publisher.Publish(new MarcoMessage());
                    }
                    finally
                    {
                        concurrent.Release();
                    }
                }

                await Task.Delay(200);
            }
            */

            while (true)
            {
                await Task.Delay(1000);
            }
        }
    }
}
