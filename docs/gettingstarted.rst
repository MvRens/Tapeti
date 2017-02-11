Getting started
===============

Install packages
----------------
I'll assume you are familiar with installing NuGet.org packages into your project.

Find and install the *Tapeti* package. This will also install *Tapeti.Annotations*, which contains the various attributes.

You will need an integration package as well for your IoC (Inversion of Control) container of choice. At the time of writing, one is provided for `SimpleInjector <https://simpleinjector.org/>`_. Simply install *Tapeti.SimpleInjector* as well.

.. note:: If you need support for your favourite library, implement *IDependencyContainer* using the *Tapeti.SimpleInjector* source as a reference and replace *SimpleInjectorDependencyResolver* with your class name in the example code below.

Configuring Tapeti
------------------
First create an instance of TapetiConfig, tell it which controllers to register and have it gather all the information by calling Build. Then pass the result to a TapetiConnection.

::

  using SimpleInjector;
  using Tapeti;

  internal class Program
  {
      private static void Main()
      {
          var container = new Container();
          var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
              .RegisterAllControllers()
              .Build();

          using (var connection = new TapetiConnection(config)
          {
              Params = new TapetiConnectionParams
              {
                  Host = "localhost"
              }
          })
          {
              connection.SubscribeSync();

              // Start service
          }
      }
  }

.. note:: RegisterAllControllers without parameters searches the entry assembly. Pass an Assembly parameter to register other or additional controllers.

.. caution:: Tapeti attempts to register it's default implementations in the IoC container during configuration, as well as when starting the connection (to register IPublisher). If your container is immutable after the initial configuration, like SimpleInjector is, make sure that you run the Tapeti configuration before requesting any instances from the container.

Defining a message
------------------
A message is a plain object which can be serialized using `Json.NET <http://www.newtonsoft.com/json>`_.

::

    public class SomethingHappenedMessage
    {
        public string Description { get; set; }
    }

Creating a message controller
-----------------------------

.. error:: You've stumbled upon a piece of unfinished documentation.
   Behind you is all prior knowledge. In front of you is nothing but emptyness. What do you do?

   1. Attempt to explore further
   2. Complain to the author and demand your money back
   3. Abandon all hope

   > |