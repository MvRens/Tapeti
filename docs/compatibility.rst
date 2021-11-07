Compatibility
=============

ASP.NET Core
------------
When integrating Tapeti into an ASP.NET Core service, depending on your naming conventions you may run into an issue where ASP.NET tries to register all your messaging controllers as API controllers. This is because by default any class ending in "Controller" will be picked up by ASP.NET.

You can rename your Tapeti controller classes as long as there are no persisted Tapeti Flows for that controller.

Alternatively, you can filter the classes that ASP.NET will register by using a custom ControllerFeatureProvider.


Whitelist ASP.NET controllers
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
If all your ASP.NET controllers use the [ApiController] attribute, you can simply whitelist classes which are annotated with that attribute, ensuring the class name is not relevant to ASP.NET:

::

    public class APIControllersOnlyFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            return typeInfo.IsClass && typeInfo.IsPublic && !typeInfo.IsAbstract &&
                typeInfo.GetCustomAttribute<ApiControllerAttribute>() != null;
        }
    }


Blacklist Tapeti controllers
^^^^^^^^^^^^^^^^^^^^^^^^^^^^
If instead you want the default ASP.NET behaviour but only exclude Tapeti messaging controllers, you can decorate the default feature provider as follows:

::

    public class ExcludeMessagingControllerFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            return base.IsController(typeInfo) &&
                typeInfo.GetCustomAttribute<MessageControllerAttribute>() == null;
        }
    }


Replace feature provider
^^^^^^^^^^^^^^^^^^^^^^^^
In either case, replace the default ControllerFeatureProvider in the ConfigureServices method of your startup class:

::

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    var controllerFeatureProvider = manager.FeatureProviders
                        .FirstOrDefault(provider => provider is ControllerFeatureProvider);

                    if (controllerFeatureProvider != null)
                        manager.FeatureProviders.Remove(controllerFeatureProvider);

                    manager.FeatureProviders.Add(new APIControllersOnlyFeatureProvider());
                    // or: manager.FeatureProviders.Add(new ExcludeMessagingControllerFeatureProvider());
                });
        }