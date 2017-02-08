# Tapeti
> 'Small to medium-sized and classified as "Least Concern" by the IUCN.'
>
> [_Wikipedia_](https://en.wikipedia.org/wiki/Tapeti)

Tapeti is a wrapper for the RabbitMQ .NET client designed for long-running microservices with a few specific goals:

1. Automatic registration of message handlers
2. Publishing without transport details, exchange and routing key determined by strategies
3. Attribute based, no base class requirements (only for convenience)
4. Graceful handling of connection issues, even at startup
5. Support for message flow using an extension package