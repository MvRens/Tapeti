## Introduction
Tapeti is a wrapper for the RabbitMQ .NET Client designed for long-running microservices. It’s main goal is to minimize the amount of messaging code required, and instead focus on the higher-level flow.

## Key features
* Consumers are declared using MVC-style controllers and are registered automatically based on annotations
* Publishing requires only the message class, no transport details such as exchange and routing key
* Flow extension (stateful request - response handling with support for parallel requests)
* No inheritance required
* Graceful recovery in case of connection issues, and in contrast to most libraries not designed for services, during startup as well
* Extensible using middleware

## Show me the code!
Below is a bare minimum message controller from the first example project to get a feel for how messages are handled using Tapeti.
```csharp
/// <summary>
/// Example of a simple broadcast message used in the standard publish - subscribe pattern
/// </summary>
public class PublishSubscribeMessage
{
    [Required(ErrorMessage = "Don't be impolite, supply a {0}")]
    public string Greeting { get; set; }
}


[MessageController]
[DynamicQueue("tapeti.example.01")]
public class ExampleMessageController
{
    public ExampleMessageController() { }

    public void HandlePublishSubscribeMessage(PublishSubscribeMessage message)
    {
        Console.WriteLine("Received message: " + message.Greeting);
    }
}
```

More details and examples can be found in the documentation as well as the example projects included with the source.

## Documentation
The documentation for Tapeti is available on Read the Docs:

[Master branch (stable release)](http://tapeti.readthedocs.io/en/stable/introduction.html)<br />
[![Documentation Status](https://readthedocs.org/projects/tapeti/badge/?version=stable)](http://tapeti.readthedocs.io/en/stable/introduction.html?badge=stable)

[Develop branch](http://tapeti.readthedocs.io/en/latest/introduction.html)<br />
[![Documentation Status](https://readthedocs.org/projects/tapeti/badge/?version=latest)](http://tapeti.readthedocs.io/en/latest/introduction.html?badge=latest)



## Builds
Builds are automatically run using AppVeyor, with the resulting packages being pushed to NuGet.

Master build (stable release)
[![Build status](https://ci.appveyor.com/api/projects/status/cyuo0vm7admy0d9x/branch/master?svg=true)](https://ci.appveyor.com/project/MvRens/tapeti/branch/master)

Latest build
[![Build status](https://ci.appveyor.com/api/projects/status/cyuo0vm7admy0d9x?svg=true)](https://ci.appveyor.com/project/MvRens/tapeti)