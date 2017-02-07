namespace Tapeti.Flow
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithFlow(this TapetiConfig config)
        {
            config.Use(new FlowMiddleware());
            return config;
        }
    }
}
