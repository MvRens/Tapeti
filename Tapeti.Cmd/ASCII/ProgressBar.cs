using System;
using System.Text;

namespace Tapeti.Cmd.ASCII
{
	public class ProgressBar : IDisposable, IProgress<int>
    {
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(20);

        private readonly int max;
        private readonly int width;
        private readonly bool showPosition;
        private int position;

        private readonly bool enabled;
        private DateTime lastUpdate = DateTime.MinValue;
        private int lastOutputLength;

        
		public ProgressBar(int max, int width = 10, bool showPosition = true)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero");
            
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than zero");
            
            this.max = max;
            this.width = width;
            this.showPosition = showPosition;

            enabled = !Console.IsOutputRedirected;
            if (!enabled) 
                return;
            
            Console.CursorVisible = false;
            Redraw();
        }


        public void Dispose()
        {
            if (!enabled || lastOutputLength <= 0) 
                return;
            
            Console.CursorLeft = 0;
            Console.Write(new string(' ', lastOutputLength));
            Console.CursorLeft = 0;
            Console.CursorVisible = true;
        }


        public void Report(int value)
        {
            if (!enabled)
                return;

            value = Math.Max(0, Math.Min(max, value));
            position = value;

            var now = DateTime.Now;
            if (now - lastUpdate < UpdateInterval)
                return;

            lastUpdate = now;
            Redraw();
        }

        
		private void Redraw()
        {
            var output = new StringBuilder("[");
                
            var blockCount = (int)Math.Truncate((decimal)position / max * width);
            if (blockCount > 0)
                output.Append(new string('#', blockCount));

            if (blockCount < width)
                output.Append(new string('.', width - blockCount));

            output.Append("] ");

            if (showPosition)
            {
                output
                    .Append(position.ToString("N0")).Append(" / ").Append(max.ToString("N0"))
                    .Append(" (").Append((int) Math.Truncate((decimal) position / max * 100)).Append("%)");
            }
            else
                output.Append(" ").Append((int)Math.Truncate((decimal)position / max * 100)).Append("%");


            var newLength = output.Length;    
            if (newLength < lastOutputLength)
                output.Append(new string(' ', lastOutputLength - output.Length));

            Console.CursorLeft = 0;
            Console.Write(output);

            lastOutputLength = newLength;
        }
    }
}
