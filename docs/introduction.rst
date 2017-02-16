Introduction
============

    | 'Small to medium-sized and classified as "Least Concern" by the IUCN.'
    | `Wikipedia <https://en.wikipedia.org/wiki/Tapeti>`_

Tapeti is a wrapper for the RabbitMQ .NET Client designed for long-running microservices. It's main goal is to minimize the amount of messaging code required, and instead focus on the higher-level flow.

Tapeti requires at least .NET 4.6.1.

Key features
------------

* Consumers are declared using MVC-style controllers and are registered automatically based on annotations
* Publishing requires only the message class, no transport details such as exchange and routing key
* No inheritance required
* Graceful recovery in case of connection issues, and in contrast to most libraries not designed for services, during startup as well
* Extensible using middleware, see for example the Tapeti Flow package


What it is not
--------------
Tapeti is not a general purpose RabbitMQ client. Although some behaviour can be overridden by implementing various interfaces, it enforces it's style of messaging and assumes everyone on the bus speaks the same language.


What is missing
---------------
Durable queues are not created and bound automatically yet. The assumption is made that these queues are initialized during a deploy to ensure messages are persisted even when the consuming service isn't running yet.

The author shamelessly plugs `RabbitMetaQueue <https://github.com/PsychoMark/RabbitMetaQueue>`_, which will probably be integrated into Tapeti at one point.

Furthermore there are hardly any unit tests yet. This will require a bit more decoupling in the lower levels of the Tapeti code.
