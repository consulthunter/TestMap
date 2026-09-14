using System.Reflection;
using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.Configuration;

/// <summary>
/// Produces a per-attempt copy of a provider config with the model (and, where the provider has
/// one, the endpoint) overridden.
///
/// The configs hanging off AiProviderConfig are shared mutable singletons that every attempt in a
/// run reads through. Setting Model on one in place to serve a single arm would leak that model
/// into every later attempt, so overrides must always go through a clone.
/// </summary>
public static class AiProviderConfigOverrides
{
    private const string EndpointPropertyName = "Endpoint";

    /// <summary>
    /// Returns <paramref name="source"/> unchanged when neither override applies, otherwise a
    /// shallow clone carrying them. Cloning copies every public settable property, so
    /// provider-specific credentials (OrgId, AwsRegion, TokenPath, ...) travel with the copy.
    /// </summary>
    public static IAiProviderConfig Apply(IAiProviderConfig source, string? model, string? endpoint)
    {
        ArgumentNullException.ThrowIfNull(source);

        var appliesModel = !string.IsNullOrWhiteSpace(model) &&
                           !string.Equals(model, source.Model, StringComparison.Ordinal);
        var appliesEndpoint = !string.IsNullOrWhiteSpace(endpoint) &&
                              !string.Equals(endpoint, ReadEndpoint(source), StringComparison.Ordinal);

        if (!appliesModel && !appliesEndpoint)
            return source;

        var clone = Clone(source);

        if (appliesModel)
            clone.Model = model!;

        if (appliesEndpoint && !TryWriteEndpoint(clone, endpoint!))
            throw new InvalidOperationException(
                $"Provider '{source.Provider}' has no endpoint to override. Remove Endpoint from the arm, " +
                "or use a provider that supports one (CustomOpenAi, Ollama, GoogleGemini, GoogleCloud).");

        return clone;
    }

    private static IAiProviderConfig Clone(IAiProviderConfig source)
    {
        var type = source.GetType();
        var clone = Activator.CreateInstance(type) as IAiProviderConfig
                    ?? throw new InvalidOperationException(
                        $"Provider config type '{type.Name}' could not be constructed for cloning.");

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;

            property.SetValue(clone, property.GetValue(source));
        }

        return clone;
    }

    private static string? ReadEndpoint(IAiProviderConfig config)
    {
        var property = config.GetType().GetProperty(EndpointPropertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.CanRead == true && property.PropertyType == typeof(string)
            ? property.GetValue(config) as string
            : null;
    }

    private static bool TryWriteEndpoint(IAiProviderConfig config, string endpoint)
    {
        var property = config.GetType().GetProperty(EndpointPropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite != true || property.PropertyType != typeof(string))
            return false;

        property.SetValue(config, endpoint);
        return true;
    }
}
