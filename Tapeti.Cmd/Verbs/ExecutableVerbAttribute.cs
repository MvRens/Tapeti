using System;
using Tapeti.Cmd.ConsoleHelper;

namespace Tapeti.Cmd.Verbs
{
    /// <remarks>
    /// Implementations are expected to have a constructor which accepts the options class
    /// associated with the ExecutableVerb attribute.
    /// </remarks>
    public interface IVerbExecuter
    {
        void Execute(IConsole console);
    }
    
    
    
    [AttributeUsage(AttributeTargets.Class)]
    public class ExecutableVerbAttribute : Attribute
    {
        public Type VerbExecuter { get; }

        
        public ExecutableVerbAttribute(Type verbExecuter)
        {
            if (!typeof(IVerbExecuter).IsAssignableFrom(verbExecuter))
                throw new InvalidCastException("Type must support IVerbExecuter");
            
            VerbExecuter = verbExecuter;
        }
    }
}
