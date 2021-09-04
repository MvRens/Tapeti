using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using CommandLine;
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

            CommandLine.Parser.Default.ParseArguments(args, verbTypes.ToArray())
                .WithParsed(o =>
                {
                    try
                    {
                        var executableVerbAttribute = o.GetType().GetCustomAttribute<ExecutableVerbAttribute>();
                        var executer = Activator.CreateInstance(executableVerbAttribute.VerbExecuter, o) as IVerbExecuter;

                        // Should have been validated by the ExecutableVerbAttribute
                        Debug.Assert(executer != null, nameof(executer) + " != null");
                        
                        executer.Execute();
                        exitCode = 0;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        DebugConfirmClose();
                    }
                })
                .WithNotParsed(_ =>
                {
                    DebugConfirmClose();
                });
                
            return exitCode;
        }


        private static void DebugConfirmClose()
        {
            if (!Debugger.IsAttached)
                return;

            Console.WriteLine("Press any Enter key to continue...");
            Console.ReadLine();
        }
    }
}
