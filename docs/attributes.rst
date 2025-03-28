Attributes
==========

As described in previous chapters, Tapeti uses attributes to handle configuration which can differ per controller, queue or message. This chapter provides a reference, as well as documentation for some of the more advanced attributes.

There are two types of attributes used by Tapeti: message annotations and client side configuration.

Message annotations
-------------------

The attributes control how a message should be published or consumed. They are typically referenced in an assembly containing the message classes which is shared between the sending and receiving services.

These annotations are part of the separate Tapeti.Annotations NuGet package.

Mandatory
^^^^^^^^^
Indicates the message must be handled by at least one queue. An exception will be thrown on the publishing side if there is no route for the message.

See: :ref:`mandatory`

::

    [Mandatory]
    public class SomeoneHandleMeMessage
    {
    }

Request
^^^^^^^
Indicates the message is a request which expects a response. An exception will be thrown on the consuming side if the message handler does not return the specified response type.

See: :ref:`requestresponse`

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


RoutingKey
^^^^^^^^^^
Modifies the default routing key that is generated for this message. Either specify the 'Full' property to override the routing key completely, or specify a 'Prefix' and/or 'Postfix'.

.. note::
    If a non-default IRoutingKeyStrategy is used, it's implementation must explicitly support this attribute for it to have an effect, preferably by using Tapeti.Default.RoutingKeyHelper.

::

    [RoutingKey(Full = "modified.routing.key")]
    public class SomeMessage
    {
    }


Client configuration
--------------------

These attributes control how the Tapeti client handles messages.

DedicatedChannel
^^^^^^^^^^^^^^^^
Requests a dedicated RabbitMQ Channel for consuming messages from the queue.
    
The DedicatedChannel attribute can be applied to any controller or method and will apply to the queue
that is used in that context. It does not need be applied to all message handlers for that queue to have
an effect.

The intended use case is for high-traffic message handlers, or message handlers which can block for either
a long time or indefinitely for throttling purposes. These can clog up the channel's workers and impact
other queues.

DurableQueue
^^^^^^^^^^^^
Binds to an existing durable queue to receive messages. Can be used on an entire MessageController class or on individual methods.

DynamicQueue
^^^^^^^^^^^^
Creates a non-durable auto-delete queue to receive messages. Can be used on an entire MessageController class or on individual methods.

MessageController
^^^^^^^^^^^^^^^^^
Attaching this attribute to a class includes it in the auto-discovery of message controllers
when using the RegisterAllControllers method. It is not required when manually registering a controller.

NoBinding
^^^^^^^^^
Indicates that the method is not a message handler and should not be bound by Tapeti.

QueueArguments
^^^^^^^^^^^^^^
Specifies the optional queue arguments (also known as 'x-arguments') used when declaring
the queue.

The QueueArguments attribute can be applied to any controller or method and will affect the queue
that is used in that context. For durable queues, at most one QueueArguments attribute can be specified
per unique queue name.

Also note that queue arguments can not be changed after a queue is declared. You should declare a new queue
and make the old one Obsolete to have Tapeti automatically removed it once it is empty. Tapeti will use the
existing queue, but log a warning at startup time.

ResponseHandler
^^^^^^^^^^^^^^^
Indicates that the method only handles response messages which are sent directly
to the queue. No binding will be created.