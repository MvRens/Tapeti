using System;
using Tapeti.Config;

namespace Tapeti.Transient
{
    /// <summary>
    /// TapetiConfig extension to register Tapeti.Transient
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Registers the transient publisher and required middleware
        /// </summary>
        /// <param name="config"></param>
        /// <param name="defaultTimeout"></param>
        /// <param name="dynamicQueuePrefix"></param>
        /// <returns></returns>
        public static ITapetiConfigBuilder WithTransient(this ITapetiConfigBuilder config, TimeSpan defaultTimeout, string dynamicQueuePrefix = "transient")
        {
            config.Use(new TransientExtension(defaultTimeout, dynamicQueuePrefix));
            return config;
        }
    }
}