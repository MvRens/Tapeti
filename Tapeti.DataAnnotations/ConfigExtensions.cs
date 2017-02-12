namespace Tapeti.DataAnnotations
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithDataAnnotations(this TapetiConfig config)
        {
            config.Use(new DataAnnotationsMiddleware());
            return config;
        }
    }
}
