Introduction
============

    | 'Small to medium-sized and classified as "Least Concern" by the IUCN.'
    | `Wikipedia <https://en.wikipedia.org/wiki/Tapeti>`_

Tapeti is a wrapper for the RabbitMQ .NET Client designed for long-running microservices. It's main goal is to minimize the amount of messaging code required, and instead focus on the higher-level flow.

Tapeti is built using .NET Standard 2.0 and mostly tested using .NET 4.7.

Key features
------------

* Consumers are declared using MVC-style controllers and are registered automatically based on annotations
* Publishing requires only the message class, no transport details such as exchange and routing key
* :doc:`flow` (stateful request - response handling with support for parallel requests)
* No inheritance required
* Graceful recovery in case of connection issues, and in contrast to most libraries not designed for services, during startup as well
* Extensible using middleware


What it is not
--------------
Tapeti is not a general purpose RabbitMQ client. Although some behaviour can be overridden by implementing various interfaces, it enforces it's style of messaging and assumes everyone on the bus speaks the same language.