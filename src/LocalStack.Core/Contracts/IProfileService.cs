namespace LocalStack.Core.Contracts;

public interface IProfileService
{
    Task<ProfileServiceResult> CreateProfileAsync(AddProfileModel addProfileModel);
}