using JetBrains.Annotations;
using Tapeti.Config;
using Tapeti.Tests.Mock;

namespace Tapeti.Tests.Config
{
    public class BaseControllerTest
    {
        protected readonly MockDependencyResolver DependencyResolver = new();


        protected ITapetiConfig GetControllerConfig<[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)] T>() where T : class
        {
            var configBuilder = new TapetiConfig(DependencyResolver);

            configBuilder.EnableDeclareDurableQueues();
            configBuilder.RegisterController(typeof(T));
            var config = configBuilder.Build();

            return config;
        }


        protected ITapetiConfigBindings GetControllerBindings<[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)] T>() where T : class
        {
            return GetControllerConfig<T>().Bindings;
        }
    }
}