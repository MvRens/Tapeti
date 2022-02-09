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
    internal class DataAnnotationsMessageMiddleware : IMessageMiddleware
    {
        /// <inheritdoc />
        public ValueTask Handle(IMessageContext context, Func<ValueTask> next)
        {
            var validationContext = new ValidationContext(context.Message);
            Validator.ValidateObject(context.Message, validationContext, true);

            return next();
        }
    }
}
