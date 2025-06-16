using System;
using Shouldly;
using Xunit;

namespace Tapeti.Tests.Helpers
{
    public class TapetiConnectionParamsTest
    {
        [Fact]
        public void SimpleHostname()
        {
            AssertUri("amqp://rabbit.com", new TapetiConnectionParams
            {
                HostName = "rabbit.com"
            });
        }


        [Fact]
        public void LongHostname()
        {
            AssertUri("amqp://rabbit.1234.internal.prod.Uname-IT.com", new TapetiConnectionParams
            {
                HostName = "rabbit.1234.internal.prod.Uname-IT.com"
            });
        }

        [Fact]
        public void HostnameAndPort()
        {
            AssertUri("amqp://rabbit.com:60001", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Port = 60001
            });
        }

        [Fact]
        public void HostnameAndVirtualHost()
        {
            AssertUri("amqp://rabbit.com/staging", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                VirtualHost = "/staging"
            });
        }

        [Fact]
        public void HostnameAndPrefetchCount()
        {
            AssertUri("amqp://rabbit.com?PrefetchCount=5", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                PrefetchCount = 5
            });
        }

        [Fact]
        public void HostnameAndUsername()
        {
            AssertUri("amqp://myApplication@rabbit.com", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication"
            });
        }

        [Fact]
        public void HostnameAndUsernameAndPassword()
        {
            AssertUri("amqp://myApplication:abcd1234@rabbit.com?", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "abcd1234"
            });
        }

        [Fact]
        public void HostnameAndUsernameAndFunnyPassword()
        {
            AssertUri("amqp://myApplication:a%23%24%25h%5E5Gs%21%60~EE357%28-%2B%29%3D_%5C%2F%26%2A%7C6%24%3C%3E%7B%7D%5B%5DMyFaFoRiTePeTnAmE@rabbit.com", new TapetiConnectionParams
            {
                HostName = "rabbit.com",
                Username = "myApplication",
                Password = "a#$%h^5Gs!`~EE357(-+)=_\\/&*|6$<>{}[]MyFaFoRiTePeTnAmE"
            });
        }


        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private static void AssertUri(string uri, TapetiConnectionParams expected)
        {
            var parsed = new TapetiConnectionParams(new Uri(uri));

            parsed.HostName.ShouldBe(expected.HostName, StringCompareShould.IgnoreCase);
            parsed.Port.ShouldBe(expected.Port);
            parsed.VirtualHost.ShouldBe(expected.VirtualHost);
            parsed.Username.ShouldBe(expected.Username);
            parsed.Password.ShouldBe(expected.Password);
            parsed.PrefetchCount.ShouldBe(expected.PrefetchCount);
        }
    }
}
