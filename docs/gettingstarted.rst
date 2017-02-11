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
.. error:: You've stumbled upon a piece of unfinished documentation.
   Behind you is all prior knowledge. In front of you is nothing but emptyness. What do you do?

   1. Attempt to explore further
   2. Complain to the author and demand your money back
   3. Abandon all hope

   > |

.. caution:: Tapeti attempts to register it's default implementations in the IoC container during configuration, as well as when starting the connection (to register IPublisher). If your container is immutable after the initial configuration, like SimpleInjector is, make sure that you run the Tapeti configuration before requesting any instances from the container.