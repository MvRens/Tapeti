﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    /// <inheritdoc />
    /// <summary>
    /// Validates published messages using System.ComponentModel.DataAnnotations
    /// </summary>
    public class DataAnnotationsPublishMiddleware : IPublishMiddleware
    {
        /// <inheritdoc />
        public Task Handle(IPublishContext context, Func<Task> next)
        {
            var validationContext = new ValidationContext(context.Message);
            Validator.ValidateObject(context.Message, validationContext, true);

            return next();
        }
    }
}
