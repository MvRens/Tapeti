In-depth
========


Messages
--------
As described in the Getting started guide, a message is a plain object which can be serialized using `Json.NET <http://www.newtonsoft.com/json>`_.

When communicating between services it is considered best practice to define messages in separate class library assemblies which can be referenced in other services. This establishes a public interface between services and components without binding to the implementation.


Routing keys
------------
The routing key is determined by converting CamelCase to dot-separated lowercase, leaving out "Message" at the end if it is present. In the example below, the routing key will be "something.happened":

::

  public class SomethingHappenedMessage
  {
      public string Description { get; set; }
  }

This behaviour is implemented using the IRoutingKeyStrategy interface. For more information about changing this, see `Overriding default behaviour`_



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