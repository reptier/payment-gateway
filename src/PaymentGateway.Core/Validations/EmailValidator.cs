using FluentValidation;
using FluentValidation.Validators;

namespace PaymentGateway.Core.Validations;

public class EmailValidator
{
    var validator = new InlineValidator<EmailValidator>;
}
