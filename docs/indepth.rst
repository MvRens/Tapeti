In-depth
========


Messages
--------
As described in the Getting started guide, a message is a plain object which can be serialized using `Json.NET <http://www.newtonsoft.com/json>`_.

When communicating between services it is considered best practice to define messages in separate class library assemblies which can be referenced in other services. This establishes a public interface between services and components without binding to the implementation.


.. _parameterbinding:

Parameter binding
-----------------
Tapeti will bind the parameters of message handler methods using the registered binding middleware.

Although stated in the Getting started guide that the first parameter is always assumed to be the message class, this is in fact handled by one of the default binding middleware implementations instead of being hardcoded in Tapeti. All of the default implementations play nice and will only apply to parameters not already bound by other middleware, making it easy to extend or change the default behaviour if desired.

In addition to the message class parameter, two additional default implementations are included:


CancellationToken
^^^^^^^^^^^^^^^^^
Similar to ASP.NET, Tapeti will bind parameters of type CancellationToken to a token which is cancelled when the connection to the RabbitMQ server is closed.

.. note:: This does not indicate whether the connection was closed by the application or lost unexpectedly, either scenario will cancel the token. This is by design, as any message in-flight will be put back on the queue and redelivered anyways.

Internally this CancellationToken is called ConnectionClosed, but any name can be used. For example:

::

      public async Task<CountResponseMessage> CountRabbits(CountRequestMessage message,
          CancellationToken cancellationToken)
      {
          var count = await rabbitRepository.Count(cancellationToken);

          return new CountRabbitsResponseMessage
          {
              Count = count
          };
      }


Dependency injection
^^^^^^^^^^^^^^^^^^^^
Any parameter not bound by any other means will be resolved using the IoC container which is passed to the TapetiConnection.

.. note:: It is considered best practice to use the constructor for dependency injection instead.


Enums
-----
Special care must be taken when using enums in messages. For example, you have several services consuming a message containing an enum field. Some services will have logic which depends on a specific value, others will not use that specific field at all.

Then later on, you want to add a new value to the enum. Some services will have to be updated, but the two examples mentioned above do not rely on the new value. As your application grows, it will become unmanageable to keep all services up-to-date.

Tapeti accounts for this scenario by using a custom deserializer for enums. Enums are always serialized to their string representation, and you should never rely on their ordinal values. When deserializing, if the sending service sends out an enum value that is unknown to the consuming service, Tapeti will deserialize it to an out-of-bounds enum value instead of failing immediately.

This effectively means that as long as your code has conditional or switch statements that can handle unknown values, or only perform direct comparisons, the existing logic will run without issues.

In addition, Tapeti does not allow you to pass the value as-is to another message, as the original string representation is lost. If it detects the out-of-bounds value in an enum field of a message being published, it will raise an exception.

However, Tapeti cannot analyze your code and the ways you use the enum field. So if you ignored all advice and used the ordinal value of the enum to store somewhere directly, you are left with the value 0xDEADBEEF, and you will now know why.


.. _declaredurablequeues:

Durable queues
--------------
Before version 2.0, and still by default in the newer versions, Tapeti assumes all durable queues are set up with the proper bindings before starting the service.

However, since this is very inconvenient you can enable Tapeti to manage durable queues as well. There are two things to keep in mind when enabling this functionality:

#) The `RabbitMQ management plugin <https://www.rabbitmq.com/management.html>`_ must be enabled
#) The queue name must be unique to the service to prevent conflicting updates to the bindings

To enable the automatic creation of durable queues, call EnableDeclareDurableQueues or SetDeclareDurableQueues on the TapetiConfig:

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .EnableDeclareDurableQueues()
      .RegisterAllControllers()
      .Build();


The queue will be bound to all message classes for which you have defined a message handler. If the queue already existed and contains bindings which are no longer valid, those bindings will be removed. Note however that if there are still messages of that type in the queue they will be consumed and cause an exception. To keep the queue backwards compatible, see the next section on migrating durable queues.


Migrating durable queues
------------------------
.. note:: This section assumes you are using EnableDeclareDurableQueues.

As your service evolves so can your message handlers. Perhaps a message no longer needs to handled, or you want to split them into another queue.

If you remove a message handler the binding will also be removed from the queue, but there may still be messages of that type in the queue. Since these have nowhere to go, they will cause an error and be lost.

Instead of removing the message handler you can mark it with the standard .NET ``[Obsolete]`` attribute:

::

  [MessageController]
  [DurableQueue("monitoring")]
  public class ObsoleteMonitoringController
  {
      [Obsolete]
      public void HandleEscapeMessage(RabbitEscapedMessage message)
      {
          // Handle the message like before, perform the necessary migration,
          // or simply ignore it if you no longer need it.
      }
  }

Messages will still be consumed from the queue as long as it exists, but the routing key binding will removed so no new messages of that type will be delivered.

The ``[Obsolete]`` attribute can also be applied to the entire controller to mark all message handlers it contains as obsolete.


If all message handlers bound to a durable queue are marked as obsolete, including other controllers bound to the same durable queue, the queue is a candidate for removal. During startup, if the queue is empty it will be deleted. This action is logged to the registered ILogger.

If there are still messages in the queue it's pending removal will be logged but the consumers will run as normal to empty the queue. The queue will then remain until it is checked again when the application is restarted.


.. _requestresponse:

Request - response
------------------
Messages can be annotated with the Request attribute to indicate that they require a response. For example:

::

  [Request(Response = typeof(BunnyCountResponseMessage))]
  public class BunnyCountRequestMessage
  {
      public string ColorFilter { get; set; }
  }

  public class BunnyCountResponseMessage
  {
      public int Count { get; set; }
  }

Message handlers processing the BunnyCountRequestMessage *must* respond with a BunnyCountResponseMessage, either directly or at the end of a Flow when using the :doc:`flow`.

::

  [MessageController]
  [DurableQueue("hutch")]
  public class HutchController
  {
      private IBunnyRepository repository;

      public HutchController(IBunnyRepository repository)
      {
          this.repository = repository;
      }

      public async Task<BunnyCountResponseMessage> HandleCountRequest(BunnyCountRequestMessage message)
      {
          return new BunnyCountResponseMessage
          {
              Count = await repository.Count(message.ColorFilter)
          };
      }
  }

Tapeti will throw an exception if a request message is published but there is no route for it. Tapeti will also throw an exception if you do not return the correct response class. This ensures consistent flow across services.

If you simply want to broadcast an event in response to a message, do not use the return value but instead call IPublisher.Publish in the message handler.


In practise your service may end up with the same message having two versions; one where a reply is expected and one where it's not. This is not considered a design flaw but a clear contract between services. It is common and recommended for the request message to inherit from the base non-request version, and implement two message handlers that internally perform the same logic.

While designing Tapeti this difference has been defined as :ref:`mandatory` which is explained below.


.. _mandatory:

Transfer of responsibility (mandatory messages)
-----------------------------------------------
When working with microservices there will be dependencies between services.

Sometimes the dependency should be on the consumer side, which is the classic publish-subscribe pattern. For example, a reporting service will often listen in on status updates from various other services to compose a combined report. The services producing the events simply broadcast the message without concerning who if anyone is listening.

Sometimes you need another service to handle or query data outside of your responsibility, and the Request - Response mechanism can be used. Tapeti ensures these messages are routed as described above.

The third pattern is what we refer to as "Transfer of responsibility". You need another service to continue your work, but a response is not required. For example, you have a REST API which receives and validates a request, then sends it to a queue to be handled by a background service.

Messages like these must not be lost, there should always be a queue bound to it to handle the message. Tapeti supports the [Mandatory] attribute for these cases and will throw an exception if there is no queue bound to receive the message:

::

  [Mandatory]
  public class SomeoneHandleMeMessage
  {
  }


Routing keys
------------
The routing key is determined by converting CamelCase to dot-separated lowercase, leaving out "Message" at the end if it is present. In the example below, the routing key will be "something.happened":

::

  public class SomethingHappenedMessage
  {
      public string Description { get; set; }
  }


This behaviour is implemented using the IRoutingKeyStrategy interface. For more information about changing this, see `Overriding default behaviour`_

.. note:: As you can see the namespace in which the message class is declared is not used in the routing key. This means you should not use the same class name twice as it may result in conflicts. The exchange strategy described below helps in differentiating between the messages, but to avoid any confusion it is still best practice to use unambiguous message class names or use another routing key strategy.

Exchanges
---------
The exchange on which the message is published and consumers are expected to bind to is determined by the first part of the namespace, skipping "Messaging" if it is present. In the example below, the exchange will be "Example":

::

  namespace Messaging.Example.Events
  {
      public class SomethingHappenedMessage
      {
          public string Description { get; set; }
      }
  }

This behaviour is implemented using the IExchangeStrategy interface. For more information about changing this, see `Overriding default behaviour`_



Overriding default behaviour
----------------------------
Various behaviours of Tapeti are implemented using interfaces which are resolved using the IoC container. Tapeti will attempt to register the default implementations, but these can easily be replaced with your own version. For example:

::

  // Nod to jsforcats.com
  public class YellItRoutingKeyStrategy : IRoutingKeyStrategy
  {
      public string GetRoutingKey(Type messageType)
      {
          return messageType.Name.ToUpper() + "!!!!";
      }
  }


  container.Register<IRoutingKeyStrategy, YellItRoutingKeyStrategy>();

The best place to register your implementation is before calling TapetiConfig.