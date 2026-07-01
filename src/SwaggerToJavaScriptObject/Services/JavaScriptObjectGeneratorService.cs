using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwaggerToJavaScriptObject.Models;

namespace SwaggerToJavaScriptObject.Services;

public class JavaScriptObjectGeneratorService
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    public GenerateResult Generate(string swaggerJson, IEnumerable<string> selectedTypeNames)
    {
        if (string.IsNullOrWhiteSpace(swaggerJson))
        {
            return new GenerateResult { Error = "Swagger JSON is empty." };
        }

        var selectedNames = selectedTypeNames.ToList();
        if (selectedNames.Count == 0)
        {
            return new GenerateResult { Error = "No types selected." };
        }

        try
        {
            using var doc = JsonDocument.Parse(swaggerJson);
            var root = doc.RootElement;

            JsonElement definitions;
            if (root.TryGetProperty("definitions", out var swagger2Defs))
            {
                definitions = swagger2Defs;
            }
            else if (root.TryGetProperty("components", out var components) &&
                     components.TryGetProperty("schemas", out var schemas))
            {
                definitions = schemas;
            }
            else
            {
                return new GenerateResult { Error = "No definitions found in the Swagger document." };
            }

            var sb = new StringBuilder();
            var allDefinitions = definitions
                .EnumerateObject()
                .ToDictionary(d => d.Name, d => d.Value, StringComparer.OrdinalIgnoreCase);

            foreach (var typeName in selectedNames)
            {
                if (definitions.TryGetProperty(typeName, out var schema))
                {
                    var resolvedSchema = ResolveElement(schema, allDefinitions, []);
                    var schemaJson = resolvedSchema?.ToJsonString(PrettyPrint) ?? "null";
                    var variableName = ToLowerFirstChar(typeName);
                    sb.AppendLine($"const {variableName}Schema = {schemaJson};");
                    sb.AppendLine();
                }
            }

            return new GenerateResult { Output = sb.ToString().TrimEnd() };
        }
        catch (JsonException ex)
        {
            return new GenerateResult { Error = $"Invalid JSON: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new GenerateResult { Error = $"Error generating output: {ex.Message}" };
        }
    }

    private static JsonNode? ResolveElement(
            JsonElement element,
            IReadOnlyDictionary<string, JsonElement> allDefinitions,
            HashSet<string> resolutionPath)
    {
        if (TryResolveReference(element, allDefinitions, out var referencedTypeName, out var referencedSchema))
        {
            if (!resolutionPath.Add(referencedTypeName))
            {
                return JsonNode.Parse(element.GetRawText());
            }

            var resolvedReference = ResolveElement(referencedSchema, allDefinitions, resolutionPath);
            resolutionPath.Remove(referencedTypeName);
            return resolvedReference;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => ResolveObject(element, allDefinitions, resolutionPath),
            JsonValueKind.Array => ResolveArray(element, allDefinitions, resolutionPath),
            _ => JsonSerializer.SerializeToNode(element)
        };
    }

    private static JsonObject ResolveObject(
        JsonElement element,
        IReadOnlyDictionary<string, JsonElement> allDefinitions,
        HashSet<string> resolutionPath)
    {
        var result = new JsonObject();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ResolveElement(property.Value, allDefinitions, resolutionPath);
        }

        return result;
    }

    private static JsonArray ResolveArray(
        JsonElement element,
        IReadOnlyDictionary<string, JsonElement> allDefinitions,
        HashSet<string> resolutionPath)
    {
        var result = new JsonArray();
        foreach (var arrayItem in element.EnumerateArray())
        {
            result.Add(ResolveElement(arrayItem, allDefinitions, resolutionPath));
        }

        return result;
    }

    private static bool TryResolveReference(
        JsonElement element,
        IReadOnlyDictionary<string, JsonElement> allDefinitions,
        out string referencedTypeName,
        out JsonElement referencedSchema)
    {
        referencedTypeName = string.Empty;
        referencedSchema = default;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("$ref", out var refProperty) ||
            refProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var reference = refProperty.GetString();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        const string swagger2Prefix = "#/definitions/";
        const string openApi3Prefix = "#/components/schemas/";
        if (reference.StartsWith(swagger2Prefix, StringComparison.Ordinal))
        {
            referencedTypeName = reference[swagger2Prefix.Length..];
        }
        else if (reference.StartsWith(openApi3Prefix, StringComparison.Ordinal))
        {
            referencedTypeName = reference[openApi3Prefix.Length..];
        }
        else
        {
            return false;
        }

        return allDefinitions.TryGetValue(referencedTypeName, out referencedSchema);
    }

    private static string ToLowerFirstChar(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}