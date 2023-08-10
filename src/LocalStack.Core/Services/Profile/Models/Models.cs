namespace LocalStack.Core.Services.Profile.Models;

public record AddProfileModel(string Name, string Email, string ProfilePicName, string ProfilePicBase64);

public record ProfileModel(string Id, string Name, string Email, string ProfilePicUrl, DateTime CreatedAt);