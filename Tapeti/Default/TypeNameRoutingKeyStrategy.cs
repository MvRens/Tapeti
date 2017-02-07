using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Tapeti.Default
{
    public class TypeNameRoutingKeyStrategy : IRoutingKeyStrategy
    {
        private readonly ConcurrentDictionary<Type, string> routingKeyCache = new ConcurrentDictionary<Type, string>();


        public string GetRoutingKey(Type messageType)
        {
            return routingKeyCache.GetOrAdd(messageType, BuildRoutingKey);
        }


        protected virtual string BuildRoutingKey(Type messageType)
        {
            // Split PascalCase into dot-separated parts. If the class name ends in "Message" leave that out.
            var words = SplitUpperCase(messageType.Name);

            if (words.Count > 1 && words.Last().Equals("Message", StringComparison.InvariantCulture))
                words.RemoveAt(words.Count - 1);

            return string.Join(".", words.Select(s => s.ToLower()));
        }


        protected static List<string> SplitUpperCase(string source)
        {
            var words = new List<string>();

            if (string.IsNullOrEmpty(source))
                return words;

            var wordStartIndex = 0;

            var letters = source.ToCharArray();
            var previousChar = char.MinValue;

            // Intentionally skip the first character
            for (var charIndex = 1; charIndex < letters.Length; charIndex++)
            {
                if (char.IsUpper(letters[charIndex]) && !char.IsWhiteSpace(previousChar))
                {
                    words.Add(new string(letters, wordStartIndex, charIndex - wordStartIndex));
                    wordStartIndex = charIndex;
                }

                previousChar = letters[charIndex];
            }

            words.Add(new string(letters, wordStartIndex, letters.Length - wordStartIndex));
            return words;
        }
    }
}
