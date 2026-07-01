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
            foreach (var typeName in selectedNames)
            {
                if (definitions.TryGetProperty(typeName, out var schema))
                {
                    var schemaJson = JsonSerializer.Serialize(schema, PrettyPrint);
                    sb.AppendLine($"const {typeName} = {schemaJson};");
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
}

