using System.Collections.Generic;

namespace Tapeti.Helpers
{
    /// <summary>
    /// Provides extension methods for dictionaries.
    /// </summary>
    public static class DictionaryHelper
    {
        /// <summary>
        /// Checks if two dictionaries are considered compatible. If either is null they are considered empty.
        /// </summary>
        public static bool NullSafeSameValues(this IReadOnlyDictionary<string, string> arguments1, IReadOnlyDictionary<string, string> arguments2)
        {
            if (arguments1 == null || arguments2 == null)
                return (arguments1 == null || arguments1.Count == 0) && (arguments2 == null || arguments2.Count == 0);

            if (arguments1.Count != arguments2.Count)
                return false;

            foreach (var pair in arguments1)
            {
                if (!arguments2.TryGetValue(pair.Key, out var value2) || value2 != arguments1[pair.Key])
                    return false;
            }

            return true;
        }
    }
}
