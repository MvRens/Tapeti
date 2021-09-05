using System;

namespace Tapeti.Cmd.ConsoleHelper
{
    /// <summary>
    /// Wraps access to the console to provide cooperation between temporary outputs like the
    /// progress bar and batch confirmations. Temporary outputs hide the cursor and will be
    /// automatically be erased and restored when a permanent writer is called.
    /// </summary>
    /// <remarks>
    /// Temporary outputs are automatically supressed when the console output is redirected.
    /// The Enabled property will reflect this.
    /// </remarks>
    public interface IConsole : IDisposable
    {
        bool Cancelled { get; }
        
        IConsoleWriter GetPermanentWriter();
        IConsoleWriter GetTemporaryWriter();
    }


    /// <summary>
    /// For simplicity outputs only support one line of text.
    /// For temporary writers, each call to WriteLine will overwrite the previous and clear any
    /// extra characters if the previous value was longer.
    /// </summary>
    public interface IConsoleWriter : IDisposable
    {
        bool Enabled { get; }

        void WriteLine(string value);
        
        /// <summary>
        /// Waits for any user input.
        /// </summary>
        void Confirm(string message);

        /// <summary>
        /// Waits for user confirmation (Y/N).
        /// </summary>
        bool ConfirmYesNo(string message);
    }
}
