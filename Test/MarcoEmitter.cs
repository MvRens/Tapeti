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
            var concurrent = new SemaphoreSlim(20);

            //for (var x = 0; x < 5000; x++)
            while (true)
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
        }
    }
}
