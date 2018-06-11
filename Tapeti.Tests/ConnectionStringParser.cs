using Tapeti;
using Tapeti.Helpers;
using Xunit;

namespace Tapet.Tests
{
    // ReSharper disable InconsistentNaming
    public class ConnectionStringParserTest
    {
        [Fact]
        public void EmptyConnectionString()
        {
            AssertConnectionString("", new TapetiConnectionParams());
        }

        [Fact]
        public void SimpleHostname()
        {
            AssertConnectionString("HostName=rabbit.com", new TapetiConnectionParams
            {
                HostName = "rabbit.com"
            });
        }


        [Fact]
        public void LongHostname()
        {
            AssertConnectionString("HostName=rabbit.1234.internal.prod.Uname-IT.com", new TapetiConnectionParams
            {
                HostName = "rabbit.1234.internal.prod.Uname-IT.com"
            });
        }

        [Fact]
        public void HostnameAndPort()
        {
            AssertConnectionString("HostName=rabbit.com;Port=60001", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Port = 60001
            });
        }

        [Fact]
        public void HostnameAndVirtualHost()
        {
            AssertConnectionString("HostName=rabbit.com;VirtualHost=/staging", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                VirtualHost = "/staging"
            });
        }

        [Fact]
        public void HostnameAndPrefetchCount()
        {
            AssertConnectionString("HostName=rabbit.com;PrefetchCount=5", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                PrefetchCount = 5
            });
        }

        [Fact]
        public void HostnameAndUsername()
        {
            AssertConnectionString("HostName=rabbit.com;Username=myApplication", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication"
            });
        }

        [Fact]
        public void HostnameAndUsernameAndPassword()
        {
            AssertConnectionString("HostName=rabbit.com;Username=myApplication;Password=abcd1234", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void HostnameAndUsernameAndFunnyPassword()
        {
            AssertConnectionString("HostName=rabbit.com;Username=myApplication;Password=a#$%h^5Gs!`~EE357(-+)=_\\/&*|6$<>{}[]MyFaFoRiTePeTnAmE", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "a#$%h^5Gs!`~EE357(-+)=_\\/&*|6$<>{}[]MyFaFoRiTePeTnAmE"
            });
        }

        [Fact]
        public void ExtraSemicolon()
        {
            AssertConnectionString("HostName=rabbit.com;Username=myApplication;Password=abcd1234;", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void ExtraMultipleSemicolon()
        {
            AssertConnectionString("HostName=rabbit.com;;;;;;;;;;;;;;Username=myApplication;Password=abcd1234;;;", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void OnlySemicolons()
        {
            AssertConnectionString(";;;", new TapetiConnectionParams
            {
            });
        }

        [Fact]
        public void DoubleQuotes()
        {
            AssertConnectionString("HostName=rabbit.com;Username=\"myApplication\"", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
            });
        }

        [Fact]
        public void DoubleQuotesInDoubleQuotes()
        {
            AssertConnectionString("HostName=rabbit.com;Username=\"\"\"myApplication\"\"\";Password=abcd1234", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "\"myApplication\"",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void CharactersInDoubleQuotes()
        {
            AssertConnectionString("HostName=rabbit.com;Username=\"Application?culture=\"\"nl-NL\"\";nr=5;\";Password=abcd1234", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "Application?culture=\"nl-NL\";nr=5;",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void DifferentOrder()
        {
            AssertConnectionString("Username=myApplication;PrefetchCount=10;Password=1234;Port=80;VirtualHost=/test;HostName=messagebus.universe.local", new TapetiConnectionParams
            {
                HostName = "messagebus.universe.local",
                Port = 80,
                VirtualHost = "/test",
                Username = "myApplication",
                Password = "1234",
                PrefetchCount = 10
            });
        }

        [Fact]
        public void DifferentOrderAndQuotes()
        {
            AssertConnectionString("Port=80;Username=myApplication;PrefetchCount=\"10\";Password=\"ab\"\"cd\";VirtualHost=\"/test\";HostName=\"messagebus.universe.local\"", new TapetiConnectionParams
            {
                HostName = "messagebus.universe.local",
                Port = 80,
                VirtualHost = "/test",
                Username = "myApplication",
                Password = "ab\"cd",
                PrefetchCount = 10
            });
        }

        private void AssertConnectionString(string connectionstring, TapetiConnectionParams expected)
        {
            var parsed = ConnectionStringParser.Parse(connectionstring);

            Assert.Equal(expected.HostName, parsed.HostName);
            Assert.Equal(expected.Port, parsed.Port);
            Assert.Equal(expected.VirtualHost, parsed.VirtualHost);
            Assert.Equal(expected.Username, parsed.Username);
            Assert.Equal(expected.Password, parsed.Password);
            Assert.Equal(expected.PrefetchCount, parsed.PrefetchCount);
        }
    }
}
