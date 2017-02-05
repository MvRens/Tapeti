using System;
using System.Threading.Tasks;

namespace Test
{
    public class Visualizer
    {
        public Task VisualizeMarco()
        {
            Console.WriteLine("Marco!");
            return Task.CompletedTask;
        }

        public Task VisualizePolo()
        {
            Console.WriteLine("Polo!");
            return Task.CompletedTask;
        }
    }
}
