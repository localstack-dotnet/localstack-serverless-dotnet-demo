namespace LocalStack.Core.Services.Profile.Models;

public class AddProfileModelValidator : AbstractValidator<AddProfileModel>
{
    public AddProfileModelValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.ProfilePicName).NotEmpty();
        RuleFor(x => x.ProfilePicBase64).NotEmpty();
    }
}