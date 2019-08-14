using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    /// <inheritdoc />
    /// <summary>
    /// Validates consumed messages using System.ComponentModel.DataAnnotations
    /// </summary>
    public class DataAnnotationsMessageMiddleware : IMessageMiddleware
    {
        /// <inheritdoc />
        public Task Handle(IMessageContext context, Func<Task> next)
        {
            var validationContext = new ValidationContext(context.Message);
            Validator.ValidateObject(context.Message, validationContext, true);

            return next();
        }
    }
}
