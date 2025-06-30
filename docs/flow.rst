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
To enable the use of Tapeti Flow, install the Tapeti.Flow NuGet package and call ``WithFlow()`` when setting up your TapetiConfig:

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlow()
      .RegisterAllControllers()
      .Build();

Starting a flow
---------------
To start a new flow you need to obtain an IFlowStarter from your IoC container. It has one method in various overloads: ``Start``.

Flow requires all methods participating in the flow, including the starting method, to be in the same controller. This allows the state to be stored and restored when the flow continues. The ``IFlowStarter.Start`` call does not need to be in the controller class.

The controller type is passed as a generic parameter. The first parameter to the Start method is a method selector. This defines which method in the controller is called as soon as the flow is initialised.

::

  await flowStart.Start<QueryBunniesController>(c => c.StartFlow);

The start method can have any name, but must be annotated with the ``[Start]`` attribute. This ensures it is not recognized as a message handler. The start method and any further continuation methods must return either Task<IYieldPoint> (for asynchronous methods) or simply IYieldPoint (for synchronous methods).

::

  [MessageController]
  [DynamicQueue]
  public class QueryBunniesController
  {
      public DateTime RequestStart { get; set; }

      [Start]
      public IYieldPoint StartFlow()
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
      public IYieldPoint StartFlow(string colorFilter)
      {
          RequestStart = DateTime.UtcNow();
      }
  }


.. note::
    Every time a flow is started or continued a new instance of the controller is created. All public fields in the controller are considered part of the state and will be restored when a response arrives, private and protected fields are not. Public fields must be serializable to JSON (using JSON.NET) to retain their value when a flow continues. Try to minimize the amount of state as it is cached in memory until the flow ends.

    Within a single flow message handlers are guaranteed to run sequentially to ensure the thread-safety of the flow state.


Continuing a flow
-----------------
When starting a flow you're most likely want to start with a request message. Similarly, when continuing a flow you have the option to follow it up with another request and prolong the flow. This behaviour is controlled by the IYieldPoint that must be returned from the start and continuation handlers. To get an IYieldPoint you need to inject the IFlowProvider into your controller.

IFlowProvider has a method ``YieldWithRequest`` which sends the provided request message and restores the controller when the response arrives, calling the response handler method you pass along to it.

The response handler must be marked with the ``[Continuation]`` attribute. This ensures it is never called for broadcast messages, only when the response for our specific request arrives. It must also return an IYieldPoint or Task<IYieldPoint> itself.

If the response handler is not asynchronous, use ``YieldWithRequestSync`` instead, as used in the example below:

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
      public IYieldPoint StartFlow(string colorFilter)
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

You can once again return a ``YieldWithRequest``, or end it.

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

.. note:: If the message that started the flow was a request message, you must end the flow with EndWithResponse or you will get an exception. Likewise, if the message was not a request message, you must end the flow with End.


Parallel requests
-----------------
When you want to send out more than one request, you could chain them in the response handler for each message. An easier way is to use ``YieldWithParallelRequest``. It returns a parallel request builder to which you can add one or more requests to be sent out, each with it's own response handler. In the end, the Yield method of the builder can be used to create a YieldPoint. It also specifies the converge method which is called when all responses have been handled.

An example:

::

  public IYieldPoint HandleBirthdayMessage(RabbitBirthdayMessage message)
  {
      var sendCardRequest = new SendCardRequestMessage
      {
          RabbitID = message.RabbitID,
          Age = message.Age,
          Style = CardStyles.Funny
      };

      var doctorAppointmentMessage = new DoctorAppointmentRequestMessage
      {
          RabbitID = message.RabbitID,
          Reason = "Yearly checkup"
      };

      return flowProvider.YieldWithParallelRequest()
          .AddRequestSync<SendCardRequestMessage, SendCardResponseMessage>(
            sendCardRequest, HandleCardResponse)

          .AddRequestSync<DoctorAppointmentRequestMessage, DoctorAppointmentResponseMessage>(
            doctorAppointmentMessage, HandleDoctorAppointmentResponse)

          .YieldSync(ContinueAfterResponses);
  }

  [Continuation]
  public void HandleCardResponse(SendCardResponseMessage message)
  {
      // Handle card response. For example, store the result in a public field
  }

  [Continuation]
  public void HandleDoctorAppointmentResponse(DoctorAppointmentResponseMessage message)
  {
      // Handle appointment response. Note that the order of the responses is not guaranteed,
      // but the handlers will never run at the same time, so it is safe to access
      // and manipulate the public fields of the controller.
  }

  private IYieldPoint ContinueAfterResponses()
  {
      // Perform further operations on the results stored in the public fields

      // This flow did not start with a request message, so end it normally
      return flowProvider.End();
  }


A few things to note:

#) The response handlers do not return an IYieldPoint themselves, but void (for AddRequestSync) or Task (for AddRequest). Therefore they can not influence the flow. Instead the converge method as passed to Yield or YieldSync determines how the flow continues. It is called immediately after the last response handler.
#) The converge method must be private, as it is not a valid message handler in itself.
#) You must add at least one request, or specify the NoRequestsBehaviour parameter for Yield/YieldSync explicitly.

Note that you do not have to perform all the operations in one go. You can store the result of ``YieldWithParallelRequest`` and conditionally call ``AddRequest`` or ``AddRequestSync`` as many times as required.


Adding requests to a parallel flow
----------------------------------
As mentioned above, you can not start a new parallel request in the same flow while the current one has not converged yet. This is enforced by the response handlers not returning an IYieldPoint.

You can however add requests to the current parallel request while handling one of the responses. This is equivalent to adding the request to the parallel flow builder initially, and will delay calling the converge method until a response has been received to this new request as well.

To add an additional request, include a second parameter in the continuation method of type IFlowParallelRequest. The continuation method also needs to be async to be able to await the IFlowParallelRequest.AddRequest[Sync] methods. For example:

::

  [Continuation]
  public async Task HandleDoctorAppointmentResponse(DoctorAppointmentResponseMessage appointment,
      IFlowParallelRequest parallelRequest)
  {
      // Now that we have the appointment details, we can query the patient data
      await parallelRequest.AddRequestSync<PatientRequestMessage, PatientResponseMessage>(
          new PatientRequestMessage
          {
              PatientID = appointment.PatientID
          },
          HandlePatientResponse);
  }



Persistent state
----------------
By default flow state is only preserved while the service is running. To persist the flow state across restarts and reboots, provide an implementation of IFlowRepository to ``WithFlow()``.

::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlow(new MyFlowStore())
      .RegisterAllControllers()
      .Build();


.. _flowsql:

SQL Server
^^^^^^^^^^

Tapeti.Flow includes a set of implementations for SQL server you can use as well. First, install the Tapeti.Flow.SQL NuGet package.
Then make sure your database contains the necessary tables to store flow state. You can run the upgrade scripts either by using a library like `DbUp`_ to run all scripts embedded in the Tapeti.Flow.SQL assembly, or by using the TapetiFlowSqlMetadata helper class:

::

    await using var sqlConnection = new SqlConnection(yourConnectionString);
    await sqlConnection.OpenAsync();

    await TapetiFlowSqlMetadata.UpdateForMultiInstanceStore(sqlConnection);

    // The above call is compatible with the 'single instance' store as well, but creates a few
    // extra tables. If you're want only the essential tables, use 
    // UpdateForSingleInstanceCachedStore instead.



Single- vs multi-instance store
'''''''''''''''''''''''''''''''

Prior to version 4.0, all active flows were always stored in memory and optionally persisted to a SQL database. This causes issues when running multiple instances of a service, where messages continuing the flow can be consumed by either instance.

Since version 4.0 a second SQL flow store is available which is compatible with running multiple instances. Depending on your use case you may want to use either. A quick comparison:


| **Single instance cached store**
| All flows are locked and cached in memory. Only mutations are performed in the SQL database without the need for database level locking.

| ✅ Better runtime performance
| ✅ Reduced SQL queries
| ❌ Start-up time increases the more flows are active

| **Multi instance store**
| The store is fully stateless. All locking is performed in a SQL table.

| ✅ Required when running multiple instances on the same queue
| ✅ No start-up cost
| ❌ Increased overhead and SQL queries


.. note::
    Flows which continue on a dynamic queue are always stored in memory only, as the dynamic queue is specific to an instance of a service. This scenario is fully supported, though not recommended, for the same reasons as the Transient extension is no longer recommended: this is almost always better handled by other RPC mechanisms like REST API calls.


Configuration
'''''''''''''

Register the SQL flow store by calling one of the ``WithFlowSql...`` extension methods before calling ``WithFlow``:

.. note::
    The WithFlowSql extension method is still available for backwards compatibility, and will use the backwards compatible single instance store. However, this is now marked as obsolete to encourage the use of the more explicit methods.


::

  var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
      .WithFlowSqlStoreMultiInstance(new SqlMultiInstanceFlowStore.Config("Server=localhost;Database=TapetiTest;Integrated Security=true"))
      .WithFlow()
      .RegisterAllControllers()
      .Build();



Backwards compatibility in flows
''''''''''''''''''''''''''''''''
The controller (including namespace) and method names for response handlers and converge methods are stored in the flow and must be valid when they are loaded again. Tapeti.Flow.SQL validates these methods at startup to prevent runtime errors when trying to continue a previously persisted flow.

To allow for refactoring the code, it is possible to specify a method to map the old name to a new value. This method is passed to the `WithFlowSql...` method:

::

    // ...
    .WithFlowSqlStoreMultiInstance(new SqlMultiInstanceFlowStore.Config("..."), 
        MapContinuationMethods)
    // ...


    private void MapContinuationMethods(IStoredContinuationMethod method)
    {
        // Use whatever logic applies to your code...
        if (method is { DeclaringTypeName: "OldController", MethodName: "OldMethod" })
            method.MapTo<NewController>(c => c.NewMethod);
    }


.. _DbUp: https://dbup.github.io/