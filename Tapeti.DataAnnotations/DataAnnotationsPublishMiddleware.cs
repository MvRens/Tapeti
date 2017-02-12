using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    public class DataAnnotationsPublishMiddleware : IPublishMiddleware
    {
        public Task Handle(IPublishContext context, Func<Task> next)
        {
            var validationContext = new ValidationContext(context.Message);
            Validator.ValidateObject(context.Message, validationContext);

            return next();
        }
    }
}
