namespace LocalStack.Core.Contracts;

public interface IProfileService
{
    Task<GetProfileServiceResult> GetProfileByIdAsync(Guid id);

    Task<CreateProfileServiceResult> CreateProfileAsync(AddProfileModel addProfileModel);
}