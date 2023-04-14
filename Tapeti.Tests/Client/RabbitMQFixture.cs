using System;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Tapeti.Tests.Client
{
    [CollectionDefinition(Name)]
    public sealed class RabbitMQCollection : ICollectionFixture<RabbitMQFixture>
    {
        public const string Name = "RabbitMQ";
    }


    public sealed class RabbitMQFixture : IAsyncLifetime
    {
        public static string RabbitMQUsername => "tapetitests";
        public static string RabbitMQPassword => "topsecret1234";

        public ushort RabbitMQPort { get; private set; }
        public ushort RabbitMQManagementPort { get; private set; }


        private TestcontainerMessageBroker? testcontainers;

        private const int DefaultRabbitMQPort = 5672;
        private const int DefaultRabbitMQManagementPort = 15672;



        private const string ImageName = "rabbitmq";
        private const string ImageTag = "3.11.3-management-alpine";


        public async Task InitializeAsync()
        {
            // Testcontainers does not seem to pull the image the first time.
            // I didn't get it to work, even using WithImagePullPolicy from the latest beta.
            // Note: running it the first time can take a while.
            var client = new DockerClientConfiguration().CreateClient();
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = ImageName,
                    Tag = ImageTag
                },
                null,
                new Progress<JSONMessage>());

            // If you get a "Sequence contains no elements" error here: make sure Docker Desktop is running
            var testcontainersBuilder = new TestcontainersBuilder<RabbitMqTestcontainer>()
                .WithMessageBroker(new RabbitMqTestcontainerConfiguration($"{ImageName}:{ImageTag}")
                {
                    Username = RabbitMQUsername,
                    Password = RabbitMQPassword
                })
                .WithExposedPort(DefaultRabbitMQManagementPort)
                .WithPortBinding(0, DefaultRabbitMQManagementPort);

            testcontainers = testcontainersBuilder.Build();

            await testcontainers!.StartAsync();

            RabbitMQPort = testcontainers.GetMappedPublicPort(DefaultRabbitMQPort);
            RabbitMQManagementPort = testcontainers.GetMappedPublicPort(DefaultRabbitMQManagementPort);
        }


        public async Task DisposeAsync()
        {
            if (testcontainers != null)
                await testcontainers.DisposeAsync();
        }
    }
}