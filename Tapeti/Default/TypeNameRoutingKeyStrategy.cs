using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tapeti.Default
{
    public class TypeNameRoutingKeyStrategy : IRoutingKeyStrategy
    {
        private const string SeparatorPattern = @"
                (?<!^) # Not start
                (
                    # Digit, not preceded by another digit
                    (?<!\d)\d 
                    |
                    # Upper-case letter, followed by lower-case letter if
                    # preceded by another upper-case letter, e.g. 'G' in HTMLGuide
                    (?(?<=[A-Z])[A-Z](?=[a-z])|[A-Z])
                )";

        private static readonly Regex SeparatorRegex = new Regex(SeparatorPattern, RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<Type, string> RoutingKeyCache = new ConcurrentDictionary<Type, string>();


        public string GetRoutingKey(Type messageType)
        {
            return RoutingKeyCache.GetOrAdd(messageType, BuildRoutingKey);
        }


        protected virtual string BuildRoutingKey(Type messageType)
        {
            // Split PascalCase into dot-separated parts. If the class name ends in "Message" leave that out.
            var words = SplitPascalCase(messageType.Name);
            if (words == null)
                return "";

            if (words.Count > 1 && words.Last().Equals("Message", StringComparison.InvariantCultureIgnoreCase))
                words.RemoveAt(words.Count - 1);

            return string.Join(".", words.Select(s => s.ToLower()));
        }

        private static List<string> SplitPascalCase(string value)
        {
            var split = SeparatorRegex.Split(value);
            if (split.Length == 0)
                return null;

            var result = new List<string>(split.Length - 1 / 2) { split[0] };
            for (var i = 1; i < split.Length; i += 2)
                result.Add(split[i] + split[i + 1]);

            return result;
        }
    }
}
