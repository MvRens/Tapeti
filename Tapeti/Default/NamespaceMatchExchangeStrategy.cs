using System;
using System.Text.RegularExpressions;

namespace Tapeti.Default
{
    public class NamespaceMatchExchangeStrategy : IExchangeStrategy
    {
        // If the namespace starts with "Messaging.Service[.Optional.Further.Parts]", the exchange will be "Service".
        // If no Messaging prefix is present, the first part of the namespace will be used instead.
        private static readonly Regex NamespaceRegex = new Regex("^(Messaging\\.)?(?<exchange>[^\\.]+)", RegexOptions.Compiled | RegexOptions.Singleline);


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
