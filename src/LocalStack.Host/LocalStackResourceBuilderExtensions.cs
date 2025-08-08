namespace LocalStack.Host.Lambda;

public static class LocalStackResourceBuilderExtensions
{
    private const string SQSEventSourceResource = "Aspire.Hosting.AWS.Lambda.SQSEventSourceResource";

    public static IDistributedApplicationBuilder ConfigureSqsEventSourceResources(this IDistributedApplicationBuilder builder, IResourceBuilder<ILocalStackResource>? localStack)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (localStack?.Resource.Options.UseLocalStack != true)
        {
            return builder;
        }


        localStack.WithAnnotation(new ResourceUrlsCallbackAnnotation(context =>
        {
            if (context.Resource is not LocalStackResource localStackResource)
            {
                return;
            }

            var localStackOptions = localStackResource.Options;

            if (!localStackOptions.UseLocalStack)
            {
                return;
            }

            var connectionString = localStackResource.ConnectionStringExpression.GetValueAsync(default).GetAwaiter().GetResult();

            if (connectionString == null)
            {
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{localStackResource.Name}' resource but the connection string was null.");
            }

            var sqsEventSourceResources = builder
                .Resources
                .Where(resource => resource.GetType().FullName == SQSEventSourceResource && resource is ExecutableResource)
                .Select(resource => (ExecutableResource)resource).ToList();

            foreach (var sqsEventSourceResourceResource in sqsEventSourceResources)
            {
                var localStackEnvCallback = new EnvironmentCallbackAnnotation(context =>
                {
                    context.EnvironmentVariables["AWS_ENDPOINT_URL"] = connectionString;
                });

                // Calculate the precise index for our new annotation based on your rules.
                var insertionIndex = GetInsertionIndex(sqsEventSourceResourceResource.Annotations);

                // Perform a surgical insert instead of just adding to the end.
                sqsEventSourceResourceResource.Annotations.Insert(insertionIndex, localStackEnvCallback);
            }
        }));

        return builder;
    }

    /// <summary>
    /// Calculates the correct index to insert our environment annotation based on a prioritized list of rules.
    /// This ensures our environment is set before other critical lifecycle annotations are processed.
    /// </summary>
    /// <param name="annotations">The existing collection of annotations on the resource.</param>
    /// <returns>The calculated index for insertion.</returns>
    private static int GetInsertionIndex(ResourceAnnotationCollection annotations)
    {
        // Rule 1: Insert right before DcpInstancesAnnotation.
        // int index = annotations.ToList().FindIndex(a => a.GetType().Name == Dcp);
        // if (index != -1) return index;

        // Rule 2: Insert after the first existing EnvironmentCallbackAnnotation.
        int index = annotations.ToList().FindLastIndex(a => a is EnvironmentCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 3: Insert after CommandLineArgsCallbackAnnotation.
        index = annotations.ToList().FindLastIndex(a => a is CommandLineArgsCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 4: Insert after ManifestPublishingCallbackAnnotation.
        index = annotations.ToList().FindLastIndex(a => a is ManifestPublishingCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 5: Insert after ResourceRelationshipAnnotation.
        index = annotations.ToList().FindLastIndex(a => a is ResourceRelationshipAnnotation);
        if (index != -1) return index + 1;

        // Rule 6 (Fallback): If none of the above markers are found, insert at the beginning.
        return 0;
    }
}