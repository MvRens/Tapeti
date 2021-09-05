using CommandLine;

namespace Tapeti.Cmd.Verbs
{
    public enum SerializationMethod
    {
        SingleFileJSON,
        EasyNetQHosepipe
    }
    
    
    public class BaseMessageSerializerOptions : BaseConnectionOptions
    {
        [Option('s', "serialization", HelpText = "The method used to serialize the message for import or export. Valid options: SingleFileJSON, EasyNetQHosepipe.", Default = SerializationMethod.SingleFileJSON)]
        public SerializationMethod SerializationMethod { get; set; }
    }
}
