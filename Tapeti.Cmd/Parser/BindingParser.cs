using System;
using System.Collections.Generic;
using System.Linq;

namespace Tapeti.Cmd.Parser
{
    public static class BindingParser
    {
        public static Tuple<string, string>[] Parse(IEnumerable<string> bindings)
        {
            return bindings
                .Select(b =>
                {
                    var parts = b.Split(':');
                    if (parts.Length != 2)
                        throw new InvalidOperationException($"Invalid binding format: {b}");

                    return new Tuple<string, string>(parts[0], parts[1]);
                })
                .ToArray();
        }
    }
}
