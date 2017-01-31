using System;
using System.Text.RegularExpressions;

namespace Tapeti.Default
{
    public class NamespaceMatchExchangeStrategy : IExchangeStrategy
    {
        public const string DefaultFormat = "^Messaging\\.(.[^\\.]+)";

        private readonly Regex namespaceRegEx;


        public NamespaceMatchExchangeStrategy(string namespaceFormat = DefaultFormat)
        {
            namespaceRegEx = new Regex(namespaceFormat, RegexOptions.Compiled | RegexOptions.Singleline);
        }


        public string GetExchange(Type messageType)
        {
            if (messageType.Namespace == null)
                throw new ArgumentException($"{messageType.FullName} does not have a namespace");

            var match = namespaceRegEx.Match(messageType.Namespace);
            if (!match.Success)
                throw new ArgumentException($"Namespace for {messageType.FullName} does not match the specified format");

            return match.Groups[1].Value.ToLower();
        }
    }
}
