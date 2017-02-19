Flow extension
==============

*Flow* in the context of Tapeti is inspired by what is referred to as a Saga or Conversation in messaging. It enables a controller to communicate with other services, temporarily yielding it's execution while waiting for a response. When the response arrives the controller will resume, retaining the original state of it's public fields.

This process is fully asynchronous, the service initiating the flow can be restarted and the flow will continue when the service is back up (assuming the queues are durable and a persistent flow state store is used).


Request - response pattern
--------------------------
Tapeti implements the request - response pattern by allowing a message handler method to simply return the response message. Tapeti Flow expands on this idea by enforcing you to explicitly declare that pattern. This is intended to prevent bugs where a flow will forever wait on a response that never comes.

Due to this requirement your messages need to follow a pattern where broadcasts (events) are separated from true request - response messages (where the reply is sent directly to the originator).

This may result in a message having two versions; one where a reply is expected and one where it's not. This is not considered a design flaw but a clear contract between services. An example is given in `Request - response versus transfer of responsibility`_.

Declaring request - response messages
-------------------------------------
A message must be annotated with the Request attribute, where the Response property declares the expected response message class.

::

  [Request(Response = RabbitCountResponseMessage)]
  public class RabbitCountRequestMessage
  {
      public int? MinimumAge { get; set; }
  }

  public class RabbitCountResponseMessage
  {
      public int Count { get; set; }
  }


Starting a flow
---------------

Ending a flow
-------------

Request - response versus transfer of responsibility
----------------------------------------------------

.. error:: You've stumbled upon a piece of unfinished documentation.
   Behind you is all prior knowledge. In front of you is nothing but emptyness. What do you do?

   1. Attempt to explore further
   2. Complain to the author and demand your money back
   3. Abandon all hope

   > |
