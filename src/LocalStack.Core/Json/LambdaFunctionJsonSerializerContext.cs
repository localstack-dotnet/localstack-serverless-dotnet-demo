namespace LocalStack.Core.Json;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ProfileServiceRequest))]
[JsonSerializable(typeof(AddProfileModel))]
[JsonSerializable(typeof(ProfileModel))]
[JsonSerializable(typeof(IServiceResponse<ProfileModel>))]
[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(SaveMessageServiceResponse))]
[JsonSerializable(typeof(SaveMessageServiceResponse[]))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
    // By using this partial class derived from JsonSerializerContext, we can generate reflection free JSON Serializer code at compile time
    // which can deserialize our class and properties. However, we must attribute this class to tell it what types to generate serialization code for.
    // See https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-source-generation
}