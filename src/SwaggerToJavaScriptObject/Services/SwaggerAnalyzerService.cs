using System.Text.Json;
using SwaggerToJavaScriptObject.Models;

namespace SwaggerToJavaScriptObject.Services;

public class SwaggerAnalyzerService
{
    public AnalyzeResult Analyze(string swaggerJson)
    {
        if (string.IsNullOrWhiteSpace(swaggerJson))
        {
            return new AnalyzeResult { Error = "Please enter a Swagger/OpenAPI JSON document." };
        }

        try
        {
            using var doc = JsonDocument.Parse(swaggerJson);
            var root = doc.RootElement;
            var types = new List<TypeDefinition>();

            // Swagger 2.0: definitions
            if (root.TryGetProperty("definitions", out var definitions))
            {
                foreach (var prop in definitions.EnumerateObject())
                {
                    types.Add(new TypeDefinition { Name = prop.Name });
                }
            }
            // OpenAPI 3.0: components.schemas
            else if (root.TryGetProperty("components", out var components) &&
                     components.TryGetProperty("schemas", out var schemas))
            {
                foreach (var prop in schemas.EnumerateObject())
                {
                    types.Add(new TypeDefinition { Name = prop.Name });
                }
            }
            else
            {
                return new AnalyzeResult
                {
                    Error = "No definitions found. Make sure the document has a 'definitions' section (Swagger 2.0) or 'components.schemas' section (OpenAPI 3.0)."
                };
            }

            return new AnalyzeResult
            {
                Types = [.. types.OrderBy(t => t.Name)]
            };
        }
        catch (JsonException ex)
        {
            return new AnalyzeResult { Error = $"Invalid JSON: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new AnalyzeResult { Error = $"Error analyzing document: {ex.Message}" };
        }
    }
}