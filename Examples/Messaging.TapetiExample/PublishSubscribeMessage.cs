using System.ComponentModel.DataAnnotations;

namespace Messaging.TapetiExample
{
    /// <summary>
    /// Example of a simple broadcast message used in the standard publish - subscribe pattern
    /// </summary>
    public class PublishSubscribeMessage
    {
        [Required(ErrorMessage = "Don't be impolite, supply a {0}")]
        public string Greeting { get; set; }
    }
}
