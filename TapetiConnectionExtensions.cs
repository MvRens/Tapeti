using System.Linq;
using System.Reflection;
using Tapeti.Annotations;

namespace Tapeti
{
    public static class TapetiConnectionExtensions
    {
        public static TapetiConnection RegisterAllControllers(this TapetiConnection connection, Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(DynamicQueueAttribute))))
                connection.RegisterController(type);

            return connection;
        }


        public static TapetiConnection RegisterAllControllers(this TapetiConnection connection)
        {
            return RegisterAllControllers(connection, Assembly.GetCallingAssembly());
        }
    }
}
