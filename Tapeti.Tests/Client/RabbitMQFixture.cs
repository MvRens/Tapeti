using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Toxiproxy.Net;
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

        private INetwork? network;
        private IContainer? rabbitMQContainer;
        private IContainer? toxiproxyContainer;

        private ushort rabbitMQPort;
        private ushort rabbitMQManagementPort;
        private ushort toxiproxyPort;

        private Toxiproxy.Net.Connection? toxiproxyConnection;
        private Toxiproxy.Net.Client? toxiproxyClient;
        private Proxy? rabbitMQProxy;
        private Proxy? rabbitMQManagementProxy;

        private readonly SemaphoreSlim acquireLimit = new(1, 1);

        private const int DefaultRabbitMQPort = 5672;
        private const int DefaultRabbitMQManagementPort = 15672;

        private const int DefaultToxiproxyPort = 8474;


        private const string RabbitMQImageName = "rabbitmq";
        private const string RabbitMQImageTag = "3.11.3-management-alpine";
        private const string ToxiproxyImageName = "ghcr.io/shopify/toxiproxy";
        private const string ToxiproxyImageTag = "2.11.0";


        public async Task InitializeAsync()
        {
            network = new NetworkBuilder()
                .WithName(Guid.NewGuid().ToString("D"))
                .Build();

            // If you get a "Sequence contains no elements" error here: make sure Docker Desktop is running
            // Yes, there is a RabbitMqBuilder in TestContainers, but this provides more control.
            rabbitMQContainer = new ContainerBuilder()
                .WithImage($"{RabbitMQImageName}:{RabbitMQImageTag}")
                .WithEnvironment("RABBITMQ_DEFAULT_USER", RabbitMQUsername)
                .WithEnvironment("RABBITMQ_DEFAULT_PASS", RabbitMQPassword)
                //.WithPortBinding(DefaultRabbitMQManagementPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
                .WithNetwork(network)
                .WithNetworkAliases("rabbitmq")
                .Build();

            toxiproxyContainer = new ContainerBuilder()
                .WithImage($"{ToxiproxyImageName}:{ToxiproxyImageTag}")
                .WithPortBinding(DefaultToxiproxyPort, true)
                .WithPortBinding(DefaultRabbitMQPort, true)
                .WithPortBinding(DefaultRabbitMQManagementPort, true)
                .WithNetwork(network)
                .Build();

            await network.CreateAsync();
            await rabbitMQContainer!.StartAsync();
            await toxiproxyContainer!.StartAsync();

            toxiproxyPort = toxiproxyContainer.GetMappedPublicPort(DefaultToxiproxyPort);
            rabbitMQPort = toxiproxyContainer.GetMappedPublicPort(DefaultRabbitMQPort);
            rabbitMQManagementPort = toxiproxyContainer.GetMappedPublicPort(DefaultRabbitMQManagementPort);

            await InitializeProxy();
        }


        /// <summary>
        /// Acquires a connection to the RabbitMQ test Toxiproxy. Be sure to Dispose to prevent blocking other tests.
        /// </summary>
        /// <remarks>
        /// This method guarantees any Toxiproxy "toxics" do not carry over into other tests.
        /// </remarks>
        public async Task<RabbitMQTestProxy> AcquireProxy()
        {
            await acquireLimit.WaitAsync();
            await toxiproxyClient!.ResetAsync();

            return new RabbitMQTestProxy(() =>
            {
                acquireLimit.Release();
            })
            {
                RabbitMQPort = rabbitMQPort,
                RabbitMQManagementPort = rabbitMQManagementPort,

                RabbitMQProxy = rabbitMQProxy!,
                RabbitMQManagementProxy = rabbitMQManagementProxy!
            };
        }


        private async Task InitializeProxy()
        {
            toxiproxyConnection = new Toxiproxy.Net.Connection("127.0.0.1", toxiproxyPort);
            toxiproxyClient = toxiproxyConnection.Client();

            rabbitMQProxy = await toxiproxyClient.AddAsync(new Proxy
            {
                Name = "RabbitMQ",
                Enabled = true,
                Listen = $"0.0.0.0:{DefaultRabbitMQPort}",
                Upstream = $"rabbitmq:{DefaultRabbitMQPort}"
            });

            rabbitMQManagementProxy = await toxiproxyClient.AddAsync(new Proxy
            {
                Name = "RabbitMQManagement",
                Enabled = true,
                Listen = $"0.0.0.0:{DefaultRabbitMQManagementPort}",
                Upstream = $"rabbitmq:{DefaultRabbitMQManagementPort}"
            });
        }


        public async Task DisposeAsync()
        {
            toxiproxyConnection?.Dispose();

            if (toxiproxyContainer != null)
                await toxiproxyContainer.DisposeAsync();

            if (rabbitMQContainer != null)
                await rabbitMQContainer.DisposeAsync();

            if (network != null)
                await network.DeleteAsync();
        }


        public sealed class RabbitMQTestProxy : IDisposable
        {
            public ushort RabbitMQPort { get; init; }
            public ushort RabbitMQManagementPort { get; init; }

            public Proxy RabbitMQProxy { get; init; } = null!;
            public Proxy RabbitMQManagementProxy { get; init; } = null!;

            private Action? OnDispose { get; }


            public RabbitMQTestProxy(Action? onDispose)
            {
                OnDispose = onDispose;
            }


            public void Dispose()
            {
                OnDispose?.Invoke();
            }
        }
    }
}