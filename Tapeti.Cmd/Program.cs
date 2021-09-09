using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using CommandLine;
using Tapeti.Cmd.ConsoleHelper;
using Tapeti.Cmd.Verbs;

namespace Tapeti.Cmd
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var exitCode = 1;
            var verbTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<ExecutableVerbAttribute>() != null)
                .ToArray();

            using var consoleWrapper = new ConsoleWrapper();

            // ReSharper disable AccessToDisposedClosure
            CommandLine.Parser.Default.ParseArguments(args, verbTypes.ToArray())
                .WithParsed(o =>
                {
                    try
                    {
                        var executableVerbAttribute = o.GetType().GetCustomAttribute<ExecutableVerbAttribute>();
                        var executer = Activator.CreateInstance(executableVerbAttribute.VerbExecuter, o) as IVerbExecuter;

                        // Should have been validated by the ExecutableVerbAttribute
                        Debug.Assert(executer != null, nameof(executer) + " != null");

                        executer.Execute(consoleWrapper);
                        exitCode = 0;
                    }
                    catch (Exception e)
                    {
                        using var consoleWriter = consoleWrapper.GetPermanentWriter();
                        consoleWriter.WriteLine(e.Message);
                        DebugConfirmClose(consoleWrapper);
                    }
                })
                .WithNotParsed(_ =>
                {
                    DebugConfirmClose(consoleWrapper);
                });
            // ReSharper restore AccessToDisposedClosure

            return exitCode;
        }


        private static void DebugConfirmClose(IConsole console)
        {
            if (!Debugger.IsAttached)
                return;

            using var consoleWriter = console.GetPermanentWriter();
            consoleWriter.Confirm("Press any key to continue...");
        }
    }
}
