using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Console = System.Console;

namespace Tapeti.Cmd.ConsoleHelper
{
    public class ConsoleWrapper : IConsole
    {
        private readonly List<TemporaryWriter> temporaryWriters = new();
        private bool temporaryActive;

        private int temporaryCursorTop;


        public ConsoleWrapper()
        {
            temporaryCursorTop = Console.CursorTop;

            Console.CancelKeyPress += (_, args) =>
            {
                if (Cancelled)
                    return;
                
                using var consoleWriter = GetPermanentWriter();
                consoleWriter.WriteLine("Cancelling...");

                args.Cancel = true;
                Cancelled = true;
            };
        }

        
        public void Dispose()
        {
            foreach (var writer in temporaryWriters)
                writer.Dispose();

            Console.CursorVisible = true;
            GC.SuppressFinalize(this);
        }


        public bool Cancelled { get; private set; }

        public IConsoleWriter GetPermanentWriter()
        {
            return new PermanentWriter(this);
        }


        public IConsoleWriter GetTemporaryWriter()
        {
            var writer = new TemporaryWriter(this, temporaryWriters.Count);
            temporaryWriters.Add(writer);

            return writer;
        }


        private void AcquirePermanent()
        {
            if (!temporaryActive)
                return;

            foreach (var writer in temporaryWriters)
            {
                Console.SetCursorPosition(0, temporaryCursorTop + writer.RelativePosition);
                writer.Clear();
            }

            Console.SetCursorPosition(0, temporaryCursorTop);
            Console.CursorVisible = true;
            temporaryActive = false;
        }


        private void ReleasePermanent()
        {
            if (temporaryWriters.Count == 0)
            {
                temporaryCursorTop = Console.CursorTop;
                return;
            }

            foreach (var writer in temporaryWriters)
            {
                writer.Restore();
                Console.WriteLine();
            }

            // Store the cursor position afterwards to account for buffer scrolling
            temporaryCursorTop = Console.CursorTop - temporaryWriters.Count;
            Console.CursorVisible = false;
            temporaryActive = true;
        }


        private void AcquireTemporary(TemporaryWriter writer)
        {
            Console.SetCursorPosition(0, temporaryCursorTop + writer.RelativePosition);
            
            if (temporaryActive) 
                return;
            
            Console.CursorVisible = false;
            temporaryActive = true;
        }


        private void DisposeWriter(BaseWriter writer)
        {
            if (writer is not TemporaryWriter temporaryWriter)
                return;
            
            Console.SetCursorPosition(0, temporaryCursorTop + temporaryWriter.RelativePosition);
            temporaryWriter.Clear();
            
            temporaryWriters.Remove(temporaryWriter);
        }


        private abstract class BaseWriter : IConsoleWriter
        {
            protected readonly ConsoleWrapper Owner;


            protected BaseWriter(ConsoleWrapper owner)
            {
                Owner = owner;
            }

            
            public virtual void Dispose()
            {
                Owner.DisposeWriter(this);
                GC.SuppressFinalize(this);
            }

            public abstract bool Enabled { get; }

            public abstract void WriteCaptured(string value, Action processInput);
            public abstract void WriteLine(string value);


            public void Confirm(string message)
            {
                WriteLine(message);

                // Clear any previous key entered before this confirmation
                while (!Owner.Cancelled && Console.KeyAvailable)
                    Console.ReadKey(true);

                while (!Owner.Cancelled && !Console.KeyAvailable)
                    Thread.Sleep(50);

                if (Owner.Cancelled)
                    return;

                Console.ReadKey(true);
            }


            public bool ConfirmYesNo(string message)
            {
                var confirmed = false;

                WriteCaptured($"{message} (Y/N) ", () =>
                {
                    // Clear any previous key entered before this confirmation
                    while (!Owner.Cancelled && Console.KeyAvailable)
                        Console.ReadKey(true);

                    var input = new StringBuilder();

                    while (!Owner.Cancelled)
                    {
                        if (!Console.KeyAvailable)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        var keyInfo = Console.ReadKey(false);

                        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault - by design
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.Enter:
                                Console.WriteLine();
                                confirmed = input.ToString().Equals("Y", StringComparison.CurrentCultureIgnoreCase);
                                return;

                            case ConsoleKey.Backspace:
                                if (input.Length > 0)
                                {
                                    input.Remove(input.Length - 1, 1);

                                    // We need to handle erasing the character ourselves, as we want to use ReadKey so that we can monitor Cancelled
                                    Console.Write(" \b");
                                }

                                break;

                            default:
                                if (keyInfo.KeyChar != -1)
                                    input.Append(keyInfo.KeyChar);

                                break;
                        }
                    }
                });

                return confirmed;
            }
        }


        private class PermanentWriter : BaseWriter
        {
            public PermanentWriter(ConsoleWrapper owner) : base(owner)
            {
            }

            
            public override bool Enabled => true;


            
            public override void WriteCaptured(string value, Action waitForInput)
            {
                Owner.AcquirePermanent();
                try
                {
                    Console.Write(value);
                    waitForInput();
                }
                finally
                {
                    Owner.ReleasePermanent();
                }
            }


            public override void WriteLine(string value)
            {
                Owner.AcquirePermanent();
                try
                {
                    Console.WriteLine(value);
                }
                finally
                {
                    Owner.ReleasePermanent();
                }
            }
        }


        private class TemporaryWriter : BaseWriter
        {
            public int RelativePosition { get; }
            
            private bool isActive;
            private string storedValue;
            

            public TemporaryWriter(ConsoleWrapper owner, int relativePosition) : base(owner)
            {
                RelativePosition = relativePosition;
            }


            public override bool Enabled => !Console.IsOutputRedirected;


            public override void WriteCaptured(string value, Action waitForInput)
            {
                WriteLine(value);
                waitForInput();
            }
            

            public override void WriteLine(string value)
            {
                if (!Enabled)
                    return;

                Owner.AcquireTemporary(this);
                Console.Write(value);

                if (!string.IsNullOrEmpty(storedValue) && storedValue.Length > value.Length)
                    // Clear characters remaining from the previous value
                    Console.Write(new string(' ', storedValue.Length - value.Length));

                storedValue = value;
                isActive = true;
            }


            public void Clear()
            {
                if (!isActive)
                    return;

                if (!string.IsNullOrEmpty(storedValue))
                    Console.Write(new string(' ', storedValue.Length));

                isActive = false;
            }


            public void Restore()
            {
                if (string.IsNullOrEmpty(storedValue))
                    return;

                Console.Write(storedValue);
                isActive = true;
            }
        }
    }
}
