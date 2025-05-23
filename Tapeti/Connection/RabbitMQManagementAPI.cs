using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tapeti.Connection;

/// <summary>
/// Provides methods for interacting with the RabbitMQ Management API.
/// </summary>
public class RabbitMQManagementAPI
{
    private readonly TapetiConnectionParams connectionParams;
    private readonly HttpClient managementClient;


    /// <inheritdoc cref="RabbitMQManagementAPI"/>
    public RabbitMQManagementAPI(TapetiConnectionParams connectionParams)
    {
        this.connectionParams = connectionParams;

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(connectionParams.Username, connectionParams.Password)
        };

        managementClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        managementClient.DefaultRequestHeaders.Add("Connection", "close");
    }


    /// <summary>
    /// Returns information about the specified queue, if it exists.
    /// </summary>
    public async Task<ManagementQueueInfo?> GetQueueInfo(string queueName)
    {
        var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
        var queuePath = Uri.EscapeDataString(queueName);

        return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}", async response =>
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<ManagementQueueInfo>(content);
        }).ConfigureAwait(false);
    }


    /// <summary>
    /// Returns a list of bindings currently declared on the specified queue.
    /// </summary>
    public async Task<IEnumerable<QueueBinding>> GetQueueBindings(string queueName)
    {
        var virtualHostPath = Uri.EscapeDataString(connectionParams.VirtualHost);
        var queuePath = Uri.EscapeDataString(queueName);

        return await WithRetryableManagementAPI($"queues/{virtualHostPath}/{queuePath}/bindings", async response =>
        {
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var bindings = JsonConvert.DeserializeObject<IEnumerable<ManagementBinding>>(content);

            // Filter out the binding to an empty source, which is always present for direct-to-queue routing
            return bindings?
                       .Where(binding => !string.IsNullOrEmpty(binding.Source) && !string.IsNullOrEmpty(binding.RoutingKey))
                       .Select(binding => new QueueBinding(binding.Source!, binding.RoutingKey!))
                   ?? [];
        }).ConfigureAwait(false);
    }


    private async Task<T> WithRetryableManagementAPI<T>(string path, Func<HttpResponseMessage, Task<T>> handleResponse)
    {
        // Workaround for: https://github.com/dotnet/runtime/issues/23581#issuecomment-354391321
        // "localhost" can cause a 1-second delay *per call*. Not an issue in production scenarios, but annoying while debugging.
        var hostName = connectionParams.HostName;
        if (hostName.Equals("localhost", StringComparison.InvariantCultureIgnoreCase))
            hostName = "127.0.0.1";

        var requestUri = new Uri($"http://{hostName}:{connectionParams.ManagementPort}/api/{path}");

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var retryDelayIndex = 0;

        while (true)
        {
            try
            {
                var response = await managementClient.SendAsync(request).ConfigureAwait(false);
                return await handleResponse(response).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (WebException e)
            {
                if (e.Response is not HttpWebResponse response)
                    throw;

                if (!TransientStatusCodes.Contains(response.StatusCode))
                    throw;
            }

            await Task.Delay(ExponentialBackoff[retryDelayIndex]).ConfigureAwait(false);

            if (retryDelayIndex < ExponentialBackoff.Length - 1)
                retryDelayIndex++;
        }
    }


    #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member - see RabbitMQ API documentation
    public class ManagementBinding
    {
        [JsonProperty("source")]
        public string? Source { get; set; }

        [JsonProperty("vhost")]
        public string? Vhost { get; set; }

        [JsonProperty("destination")]
        public string? Destination { get; set; }

        [JsonProperty("destination_type")]
        public string? DestinationType { get; set; }

        [JsonProperty("routing_key")]
        public string? RoutingKey { get; set; }

        [JsonProperty("arguments")]
        public Dictionary<string, string>? Arguments { get; set; }

        [JsonProperty("properties_key")]
        public string? PropertiesKey { get; set; }
    }


    public class ManagementQueueInfo
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("vhost")]
        public string? VHost { get; set; }

        [JsonProperty("durable")]
        public bool Durable { get; set; }

        [JsonProperty("auto_delete")]
        public bool AutoDelete { get; set; }

        [JsonProperty("exclusive")]
        public bool Exclusive { get; set; }

        [JsonProperty("arguments")]
        public Dictionary<string, JValue>? Arguments { get; set; }

        [JsonProperty("messages")]
        public uint Messages { get; set; }
    }
    #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member


    private static readonly List<HttpStatusCode> TransientStatusCodes =
    [
        HttpStatusCode.GatewayTimeout,
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.ServiceUnavailable
    ];

    private static readonly TimeSpan[] ExponentialBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(13),
        TimeSpan.FromSeconds(21),
        TimeSpan.FromSeconds(34),
        TimeSpan.FromSeconds(55)
    ];
}
