using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    /// <summary>
    /// Extends ITapetiConfigBuilder to enable DataAnnotations.
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Enables the DataAnnotations validation middleware.
        /// </summary>
        /// <param name="config"></param>
        public static ITapetiConfigBuilder WithDataAnnotations(this ITapetiConfigBuilder config)
        {
            config.Use(new DataAnnotationsExtension());
            return config;
        }
    }
}
