using System;

namespace Tapeti.Transient
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithTransient(this TapetiConfig config, TimeSpan defaultTimeout, string dynamicQueuePrefix = "transient")
        {
            config.Use(new TransientMiddleware(defaultTimeout, dynamicQueuePrefix));
            return config;
        }
    }
}