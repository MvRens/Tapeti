using Tapeti.Config;

namespace Tapeti.Flow
{
    public static class ConfigExtensions
    {
        public static ITapetiConfigBuilder WithFlow(this ITapetiConfigBuilder config, IFlowRepository flowRepository = null)
        {
            config.Use(new FlowMiddleware(flowRepository));
            return config;
        }
    }
}
