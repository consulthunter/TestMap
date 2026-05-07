using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestMap.Models.Configuration.Testing;

namespace TestMap.Services.Configuration;

public static class ConfigJsonSerializer
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new FriendlyEnumJsonConverterFactory());
        options.Converters.Add(new FrameworkConfigJsonConverter());
        return options;
    }

    private sealed class FrameworkConfigJsonConverter : JsonConverter<IFrameworkConfig>
    {
        public override IFrameworkConfig? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            var framework = JsonSerializer.Deserialize<FrameworkConfig>(ref reader, options);
            return framework ?? new FrameworkConfig();
        }

        public override void Write(Utf8JsonWriter writer, IFrameworkConfig value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, new FrameworkConfig
            {
                patterns = value.patterns ?? new List<string>()
            }, options);
        }
    }

    private sealed class FriendlyEnumJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(FriendlyEnumJsonConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    private sealed class FriendlyEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly Dictionary<TEnum, string> AliasLookup = BuildAliasLookup();
        private static readonly Dictionary<string, TEnum> NameLookup = BuildNameLookup();

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
                return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string or number for enum {typeof(TEnum).Name}.");

            var rawValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new JsonException($"Empty value is not valid for enum {typeof(TEnum).Name}.");

            var normalizedValue = Normalize(rawValue);
            if (NameLookup.TryGetValue(normalizedValue, out var resolved)) return resolved;

            throw new JsonException(
                $"Value '{rawValue}' is not valid for enum {typeof(TEnum).Name}. Allowed values: {string.Join(", ", AliasLookup.Values.OrderBy(x => x, StringComparer.Ordinal))}");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            if (!AliasLookup.TryGetValue(value, out var alias)) alias = ToKebabCase(value.ToString());

            writer.WriteStringValue(alias);
        }

        private static Dictionary<string, TEnum> BuildNameLookup()
        {
            var lookup = new Dictionary<string, TEnum>(StringComparer.Ordinal);

            foreach (var value in Enum.GetValues<TEnum>())
            {
                var enumName = value.ToString();
                lookup[Normalize(enumName)] = value;
                lookup[Normalize(ToKebabCase(enumName))] = value;

                if (AliasLookup.TryGetValue(value, out var alias)) lookup[Normalize(alias)] = value;
            }

            return lookup;
        }

        private static Dictionary<TEnum, string> BuildAliasLookup()
        {
            var aliases = new Dictionary<TEnum, string>();
            foreach (var value in Enum.GetValues<TEnum>()) aliases[value] = GetPreferredAlias(value);

            return aliases;
        }

        private static string GetPreferredAlias(TEnum value)
        {
            var enumType = typeof(TEnum);
            var enumName = value.ToString();

            if (enumType == typeof(Models.Configuration.AiProviders.AiProvider))
                return enumName switch
                {
                    "OpenAi" => "openai",
                    "CustomOpenAi" => "custom-openai",
                    "GoogleGemini" => "google-gemini",
                    "GoogleCloud" => "google-cloud",
                    _ => ToKebabCase(enumName)
                };

            return ToKebabCase(enumName);
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
                if (char.IsLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));

            return builder.ToString();
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsUpper(current))
                {
                    var hasPrevious = index > 0;
                    var nextIsLower = index + 1 < value.Length && char.IsLower(value[index + 1]);
                    var previousIsLowerOrDigit =
                        hasPrevious && (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1]));

                    if (hasPrevious && (previousIsLowerOrDigit || nextIsLower)) builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }
    }
}