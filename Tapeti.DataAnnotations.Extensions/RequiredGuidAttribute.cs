using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Tapeti.DataAnnotations.Extensions
{
    /// <summary>
    /// Can be used on Guid fields which are supposed to be Required, as the Required attribute does
    /// not work for Guids and making them Nullable is counter-intuitive.
    /// </summary>
    public class RequiredGuidAttribute : ValidationAttribute
    {
        private const string DefaultErrorMessage = "'{0}' does not contain a valid guid";
        private const string InvalidTypeErrorMessage = "'{0}' is not of type Guid";

        public RequiredGuidAttribute() : base(DefaultErrorMessage)
        {
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));

            if (value.GetType() != typeof(Guid))
                return new ValidationResult(string.Format(InvalidTypeErrorMessage, validationContext.DisplayName));

            var guid = (Guid)value;
            return guid == Guid.Empty
                ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
                : null;
        }
    }
}
