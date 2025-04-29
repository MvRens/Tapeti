using System.Text;

// ReSharper disable UnusedMember.Global - public API

namespace Tapeti.Helpers
{
    /// <summary>
    /// Helper class to construct a TapetiConnectionParams instance based on the
    /// ConnectionString syntax as used by EasyNetQ.
    /// </summary>
    public class ConnectionStringParser
    {
        private readonly TapetiConnectionParams result = new();

        private readonly string connectionstring;
        private int pos = -1;
        private char current = '\0';

        /// <summary>
        /// Parses an EasyNetQ-compatible ConnectionString into a TapetiConnectionParams instance.
        /// </summary>
        /// <param name="connectionstring"></param>
        public static TapetiConnectionParams Parse(string connectionstring)
        {
            return new ConnectionStringParser(connectionstring).result;
        }

        private ConnectionStringParser(string connectionstring)
        {
            this.connectionstring = connectionstring;

            while (MoveNext())
            {
                ParseKV();
            }
        }

        private void ParseKV()
        {
            var key = ParseKey();

            if (current != '=')
                return;

            var value = ParseValue();
            SetValue(key, value);
        }

        private string ParseKey()
        {
            var keyBuilder = new StringBuilder();
            do
            {
                switch (current)
                {
                    case '=':
                    case ';':
                        return keyBuilder.ToString();
                    case '"':
                        break;
                    default:
                        keyBuilder.Append(current);
                        break;
                }
            }
            while (MoveNext());

            return keyBuilder.ToString();
        }

        private string ParseValue()
        {
            var valueBuilder = new StringBuilder();

            MoveNext();
            if (current == '"')
            {
                while (MoveNext())
                {
                    if (current == '"')
                    {
                        // support two double quotes to be an escaped quote
                        if (!MoveNext() || current != '"')
                            break;
                    }
                    valueBuilder.Append(current);
                }
            }
            else
            {
                while (current != ';')
                {
                    valueBuilder.Append(current);
                    if (!MoveNext())
                        break;
                }
            }

            // Always leave the ';' to be consumed by the caller
            return valueBuilder.ToString();
        }

        private bool MoveNext()
        {
            var n = pos + 1;
            if (n < connectionstring.Length)
            {
                pos = n;
                current = connectionstring[pos];
                return true;
            }
            pos = connectionstring.Length;
            current = '\0';
            return false;
        }

        private void SetValue(string key, string value)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases - by design, don't fail on unknown properties
            switch (key.ToLowerInvariant()) 
            {
                case "hostname": result.HostName = value; break;
                case "port": result.Port = int.Parse(value); break;
                case "virtualhost": result.VirtualHost = value; break;
                case "username": result.Username = value; break;
                case "password": result.Password = value; break;
                case "prefetchcount": result.PrefetchCount = ushort.Parse(value); break;
                case "managementport": result.ManagementPort = int.Parse(value); break;
                case "consumerDispatchConcurrency": result.ConsumerDispatchConcurrency = ushort.Parse(value); break;
            }
        }
    }
}
