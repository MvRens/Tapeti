using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    /// <inheritdoc />
    /// <summary>
    /// Validates published messages using System.ComponentModel.DataAnnotations
    /// </summary>
    internal class DataAnnotationsPublishMiddleware : IPublishMiddleware
    {
        /// <inheritdoc />
        public ValueTask Handle(IPublishContext context, Func<ValueTask> next)
        {
            var validationContext = new ValidationContext(context.Message);
            Validator.ValidateObject(context.Message, validationContext, true);

            return next();
        }
    }
}
