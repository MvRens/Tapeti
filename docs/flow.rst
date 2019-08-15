Flow extension
==============

*Flow* in the context of Tapeti is inspired by what is referred to as a Saga or Conversation in messaging. It enables a controller to communicate with other services, temporarily yielding it's execution while waiting for a response. When the response arrives the controller will resume, retaining the original state of it's public fields.

This process is fully asynchronous, the service initiating the flow can be restarted and the flow will continue when the service is back up (assuming the queues are durable and a persistent flow state store is used).


Request - response pattern
--------------------------
Tapeti implements the request - response pattern by allowing a message handler method to simply return the response message. Tapeti Flow extends on this concept by allowing the sender of the request to maintain it's state for when the response arrives.

See :doc:`indepth` on defining request - response messages.

Enabling Tapeti Flow
--------------------
To enable the use of Tapeti Flow, install the Tapeti.Flow NuGet package and call WithFlow() when setting up your TapetiConfig:

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlow()
      .RegisterAllControllers()
      .Build();

Starting a flow
---------------
To start a new flow you need to obtain an IFlowStarter from your IoC container. It has one method in various overloads: Start.

Flow requires all methods participating in the flow, including the starting method, to be in the same controller. This allows the state to be stored and restored when the flow continues. The IFlowStarter.Start call does not need to be in the controller class.

The controller type is passed as a generic parameter. The first parameter to the Start method is a method selector. This defines which method in the controller is called as soon as the flow is initialised.

::

  await flowStart.Start<QueryBunniesController>(c => c.StartFlow);

The start method can have any name, but must be annotated with the [Start] attribute. This ensures it is not recognized as a message handler. The start method and any further continuation methods must return either Task<IYieldPoint> (for asynchronous methods) or simply IYieldPoint (for synchrnous methods).

::

  [MessageController]
  [DynamicQueue]
  public class QueryBunniesController
  {
      public DateTime RequestStart { get; set; }

      [Start]
      IYieldPoint StartFlow()
      {
          RequestStart = DateTime.UtcNow();
      }
  }



Often you'll want to pass some initial information to the flow. The Start method allows one parameter. If you need more information, bundle it in a class or struct.

::

  await flowStart.Start<QueryBunniesController>(c => c.StartFlow, "pink");

  [MessageController]
  [DynamicQueue]
  public class QueryBunniesController
  {
      public DateTime RequestStart { get; set; }

      [Start]
      IYieldPoint StartFlow(string colorFilter)
      {
          RequestStart = DateTime.UtcNow();
      }
  }


.. note:: Every time a flow is started or continued a new instance of the controller is created. All public fields in the controller are considered part of the state and will be restored when a response arrives, private and protected fields are not. Public fields must be serializable to JSON (using JSON.NET) to retain their value when a flow continues. Try to minimize the amount of state as it is cached in memory until the flow ends.

Continuing a flow
-----------------
When starting a flow you're most likely want to start with a request message. Similarly, when continuing a flow you have the option to follow it up with another request and prolong the flow. This behaviour is controlled by the IYieldPoint that must be returned from the start and continuation handlers. To get an IYieldPoint you need to inject the IFlowProvider into your controller.

IFlowProvider has a method YieldWithRequest which sends the provided request message and restores the controller when the response arrives, calling the response handler method you pass along to it.

The response handler must be marked with the [Continuation] attribute. This ensures it is never called for broadcast messages, only when the response for our specific request arrives. It must also return an IYieldPoint or Task<IYieldPoint> itself.

If the response handler is not asynchronous, use YieldWithRequestSync instead, as used in the example below:

::

  [MessageController]
  [DynamicQueue]
  public class QueryBunniesController
  {
      private IFlowProvider flowProvider;

      public DateTime RequestStart { get; set; }


      public QueryBunniesController(IFlowProvider flowProvider)
      {
          this.flowProvider = flowProvider;
      }

      [Start]
      IYieldPoint StartFlow(string colorFilter)
      {
          RequestStart = DateTime.UtcNow();

          var request = new BunnyCountRequestMessage
          {
              ColorFilter = colorFilter
          };

          return flowProvider.YieldWithRequestSync<BunnyCountRequestMessage, BunnyCountResponseMessage>
              (request, HandleBunnyCountResponse);
      }


      [Continuation]
      public IYieldPoint HandleBunnyCountResponse(BunnyCountResponseMessage message)
      {
          // Handle the response. The original RequestStart is available here as well.
      }
  }

You can once again return a YieldWithRequest, or end it.

Ending a flow
-------------
To end the flow and dispose of any stored state, return an end yieldpoint:

::

      [Continuation]
      public IYieldPoint HandleBunnyCountResponse(BunnyCountResponseMessage message)
      {
          // Handle the response.

          return flowProvider.End();
      }


Flows started by a (request) message
------------------------------------
Instead of manually starting a flow, you can also start one in response to an incoming message. You do not need access to the IFlowStarter in that case, simply return an IYieldPoint from a regular message handler:

::

  [MessageController]
  [DurableQueue("hutch")]
  public class HutchController
  {
      private IBunnyRepository repository;
      private IFlowProvider flowProvider;

      public string ColorFilter { get; set; }


      public HutchController(IBunnyRepository repository, IFlowProvider flowProvider)
      {
          this.repository = repository;
          this.flowProvider = flowProvider;
      }

      public IYieldPoint HandleCountRequest(BunnyCountRequestMessage message)
      {
          ColorFilter = message.ColorFilter;

          return flowProvider.YieldWithRequestSync<CheckAccessRequestMessage, CheckAccessResponseMessage>
            (
                new CheckAccessRequestMessage
                {
                    Username = "hutch"
                },
                HandleCheckAccessResponseMessage
            );
      }


      [Continuation]
      public IYieldPoint HandleCheckAccessResponseMessage(CheckAccessResponseMessage message)
      {
          // We must provide a response to our original BunnyCountRequestMessage
          return flowProvider.EndWithResponse(new BunnyCountResponseMessage
          {
              Count = message.HasAccess ? await repository.Count(ColorFilter) : 0
          });
  }


.. ::note If the message that started the flow was a request message, you must end the flow with EndWithResponse or you will get an exception. Likewise, if the message was not a request message, you must end the flow with End.


Parallel requests
-----------------

.. error:: You've stumbled upon a piece of unfinished documentation.
   Behind you is all prior knowledge. In front of you is nothing but emptyness. What do you do?

   1. Attempt to explore further
   2. Complain to the author and demand your money back
   3. Abandon all hope

   > |


Persistent state
----------------
By default flow state is only preserved while the service is running. To persist the flow state across restarts and reboots, provide an implementation of IFlowRepository to WithFlow().

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlow(new MyFlowRepository())
      .RegisterAllControllers()
      .Build();


Tapeti.Flow includes an implementation for SQL server you can use as well. First, make sure your database contains a table to store flow state:

::

    create table Flow
    (
        FlowID uniqueidentifier not null,
        CreationTime datetime2(3) not null,
        StateJson nvarchar(max) null,
        constraint PK_Flow primary key clustered(FlowID)
    );

Then install the Tapeti.Flow.SQL NuGet package and register the SqlConnectionFlowRepository by passing it to WithFlow, or by using the WithFlowSqlRepository extension method before calling WithFlow:

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlowSqlRepository("Server=localhost;Database=TapetiTest;Integrated Security=true")
      .WithFlow()
      .RegisterAllControllers()
      .Build();
