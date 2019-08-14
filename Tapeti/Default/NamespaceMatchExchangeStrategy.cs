using System;
using System.Text.RegularExpressions;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// IExchangeStrategy implementation which uses the first identifier in the namespace in lower case,
    /// skipping the first identifier if it is 'Messaging'.
    /// </summary>
    /// <example>
    /// Messaging.Service.Optional.Further.Parts will result in the exchange name 'service'.
    /// </example>
    public class NamespaceMatchExchangeStrategy : IExchangeStrategy
    {
        private static readonly Regex NamespaceRegex = new Regex("^(Messaging\\.)?(?<exchange>[^\\.]+)", RegexOptions.Compiled | RegexOptions.Singleline);


        /// <inheritdoc />
        public string GetExchange(Type messageType)
        {
            if (messageType.Namespace == null)
                throw new ArgumentException($"{messageType.FullName} does not have a namespace");

            var match = NamespaceRegex.Match(messageType.Namespace);
            if (!match.Success)
                throw new ArgumentException($"Namespace for {messageType.FullName} does not match the specified format");

            return match.Groups["exchange"].Value.ToLower();
        }
    }
}
