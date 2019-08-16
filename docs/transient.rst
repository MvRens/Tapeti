Transient requests
==================
The Tapeti.Transient extension provides an RPC mechanism for request - responses.

While the author does not recommend this pattern for services, opting instead for a self-hosted REST API and a discovery mechanism like `Consul <https://www.consul.io/>`_ if an immediate response is required and a queue is not (like when providing an API for a frontend application), it can still be useful in certain situations to use the Tapeti request - response mechanism and be able to wait for the response.

After enabling Tapeti.Transient you can use ``ITransientPublisher`` to send a request and await it's response, without needing a message controller.

Enabling transient requests
---------------------------
To enable the transient extension, include it in your TapetiConfig. You must provide a timeout after which the call to RequestResponse will throw an exception if no response has been received (for example, when the service handling the requests is not running).

Optionally you can also provide a prefix for the dynamic queue registered to receive responses, so you can distinguish the queue for your application in the RabbitMQ management interface. If not provided it defaults to "transient".


::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithTransient(TimeSpan.FromSeconds(30), "myapplication.transient")
      .RegisterAllControllers()
      .Build();


Using transient requests
------------------------
To send a transient request, inject ITransientPublisher into the class where you want to use it and call RequestResponse:

::

  public class BirthdayHandler
  {
      private ITransientPublisher transientPublisher;

      public BirthdayHandler(ITransientPublisher transientPublisher)
      {
          this.transientPublisher = transientPublisher;
      }

      public async Task SendBirthdayCard(RabbitInfo rabbit)
      {
          var response = await transientPublisher.RequestResponse<SendCardRequestMessage, SendCardResponseMessage>(
            new SendCardRequestMessage
            {
                RabbitID = rabbit.RabbitID,
                Age = rabbit.Age,
                Style = CardStyles.Funny
            });

          // Handle the response
      }
  }
