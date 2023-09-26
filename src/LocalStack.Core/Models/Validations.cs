namespace LocalStack.Core.Models;

public class ProfileServiceRequestValidator : AbstractValidator<ProfileServiceRequest>
{
    public ProfileServiceRequestValidator()
    {
        RuleFor(x => x.Operation).NotEmpty();
        RuleFor(x => x.Payload).NotEmpty();
    }
}