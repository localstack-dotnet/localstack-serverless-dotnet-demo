namespace LocalStack.Core.Extensions;

public static class ValidationRuleExtensions
{
    public static string ToJson(this IEnumerable<ValidationFailure> failures) =>
        JsonSerializer.Serialize(new
        {
            Status = "Failed",
            Message = "Validation errors occurred.",
            Errors = failures.Select(f => new
            {
                Property = f.PropertyName,
                Error = f.ErrorMessage
            })
        });
}