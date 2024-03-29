﻿using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Flow
{
    /// <summary>
    /// ITapetiConfigBuilder extension for enabling Flow.
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Enables Tapeti Flow.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="flowRepository">An optional IFlowRepository implementation to persist flow state. If not provided, flow state will be lost when the application restarts.</param>
        /// <returns></returns>
        public static ITapetiConfigBuilder WithFlow(this ITapetiConfigBuilder config, IFlowRepository? flowRepository = null)
        {
            config.Use(new FlowExtension(flowRepository));
            return config;
        }
    }
}
