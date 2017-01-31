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

        public Task VisualizePolo(bool matches)
        {
            Console.WriteLine(matches ? "Polo!" : "Oops! Mismatch!");
            return Task.CompletedTask;
        }
    }
}
