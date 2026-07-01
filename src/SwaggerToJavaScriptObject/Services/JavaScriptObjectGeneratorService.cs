using System.Text;
using System.Text.Json;

namespace SwaggerToJavaScriptObject.Services;

public class GenerateResult
{
    public string? Output { get; set; }
    public string? Error { get; set; }
}

public class JavaScriptObjectGeneratorService
{
    private const int MaxIndentDepth = 20;

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
            var root = doc.RootElement.Clone();

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
            foreach (var typeName in selectedNames)
            {
                if (definitions.TryGetProperty(typeName, out var schema))
                {
                    var jsValue = GenerateJsValue(schema, root, new HashSet<string>(), 0);
                    sb.AppendLine($"const {typeName} = {jsValue};");
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

    private string GenerateJsValue(JsonElement schema, JsonElement root, HashSet<string> visitedRefs, int depth)
    {
        if (depth > MaxIndentDepth)
        {
            return "{}";
        }

        // Handle $ref
        if (schema.TryGetProperty("$ref", out var refElement))
        {
            var refPath = refElement.GetString() ?? string.Empty;
            if (visitedRefs.Contains(refPath))
            {
                return "{}"; // Circular reference protection
            }

            var resolved = ResolveRef(refPath, root);
            if (resolved.HasValue)
            {
                var newVisited = new HashSet<string>(visitedRefs) { refPath };
                return GenerateJsValue(resolved.Value, root, newVisited, depth);
            }

            return "{}";
        }

        // Handle allOf, anyOf, oneOf by merging the first schema
        if (schema.TryGetProperty("allOf", out var allOf) && allOf.GetArrayLength() > 0)
        {
            return GenerateMergedJsValue(allOf, root, visitedRefs, depth);
        }
        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.GetArrayLength() > 0)
        {
            return GenerateJsValue(anyOf[0], root, visitedRefs, depth);
        }
        if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.GetArrayLength() > 0)
        {
            return GenerateJsValue(oneOf[0], root, visitedRefs, depth);
        }

        // Determine type
        string type = string.Empty;
        if (schema.TryGetProperty("type", out var typeElement))
        {
            type = typeElement.GetString() ?? string.Empty;
        }

        return type switch
        {
            "string" => GetStringDefault(schema),
            "integer" or "number" => "0",
            "boolean" => "false",
            "array" => GenerateArrayValue(schema, root, visitedRefs, depth),
            "object" => GenerateObjectValue(schema, root, visitedRefs, depth),
            _ => GenerateObjectValue(schema, root, visitedRefs, depth) // default to object
        };
    }

    private static string GetStringDefault(JsonElement schema)
    {
        // If there's an enum, use the first value
        if (schema.TryGetProperty("enum", out var enumElement) && enumElement.GetArrayLength() > 0)
        {
            var firstValue = enumElement[0];
            if (firstValue.ValueKind == JsonValueKind.String)
            {
                return $"\"{firstValue.GetString()}\"";
            }
        }
        return "\"\"";
    }

    private string GenerateArrayValue(JsonElement schema, JsonElement root, HashSet<string> visitedRefs, int depth)
    {
        if (schema.TryGetProperty("items", out var items))
        {
            var itemValue = GenerateJsValue(items, root, visitedRefs, depth + 1);
            return $"[{itemValue}]";
        }
        return "[]";
    }

    private string GenerateObjectValue(JsonElement schema, JsonElement root, HashSet<string> visitedRefs, int depth)
    {
        if (!schema.TryGetProperty("properties", out var properties))
        {
            return "{}";
        }

        var props = new List<string>();
        var indent = new string(' ', (depth + 1) * 2);
        var closingIndent = new string(' ', depth * 2);

        foreach (var prop in properties.EnumerateObject())
        {
            var propValue = GenerateJsValue(prop.Value, root, visitedRefs, depth + 1);
            props.Add($"{indent}{prop.Name}: {propValue}");
        }

        if (props.Count == 0)
        {
            return "{}";
        }

        return $"{{\n{string.Join(",\n", props)}\n{closingIndent}}}";
    }

    private string GenerateMergedJsValue(JsonElement allOf, JsonElement root, HashSet<string> visitedRefs, int depth)
    {
        // Merge all schemas from allOf into a single object
        var allProperties = new Dictionary<string, JsonElement>();

        foreach (var subSchema in allOf.EnumerateArray())
        {
            CollectProperties(subSchema, root, visitedRefs, allProperties);
        }

        if (allProperties.Count == 0)
        {
            return "{}";
        }

        var props = new List<string>();
        var indent = new string(' ', (depth + 1) * 2);
        var closingIndent = new string(' ', depth * 2);

        foreach (var (name, propSchema) in allProperties)
        {
            var propValue = GenerateJsValue(propSchema, root, visitedRefs, depth + 1);
            props.Add($"{indent}{name}: {propValue}");
        }

        return $"{{\n{string.Join(",\n", props)}\n{closingIndent}}}";
    }

    private void CollectProperties(JsonElement schema, JsonElement root, HashSet<string> visitedRefs, Dictionary<string, JsonElement> result)
    {
        // Resolve $ref first
        if (schema.TryGetProperty("$ref", out var refElement))
        {
            var refPath = refElement.GetString() ?? string.Empty;
            if (!visitedRefs.Contains(refPath))
            {
                var resolved = ResolveRef(refPath, root);
                if (resolved.HasValue)
                {
                    var newVisited = new HashSet<string>(visitedRefs) { refPath };
                    CollectProperties(resolved.Value, root, newVisited, result);
                }
            }
            return;
        }

        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var subSchema in allOf.EnumerateArray())
            {
                CollectProperties(subSchema, root, visitedRefs, result);
            }
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                result[prop.Name] = prop.Value;
            }
        }
    }

    private static JsonElement? ResolveRef(string refPath, JsonElement root)
    {
        // Only handle local references starting with #/
        if (!refPath.StartsWith("#/"))
        {
            return null;
        }

        var parts = refPath[2..].Split('/');
        JsonElement current = root;

        foreach (var part in parts)
        {
            // Unescape JSON Pointer encoding: ~1 = /, ~0 = ~
            var key = part.Replace("~1", "/").Replace("~0", "~");
            if (!current.TryGetProperty(key, out current))
            {
                return null;
            }
        }

        return current;
    }
}
