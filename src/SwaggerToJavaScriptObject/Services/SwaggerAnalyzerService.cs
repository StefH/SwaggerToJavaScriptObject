using System.Text.Json;
using RamlToOpenApiConverter;
using SwaggerToJavaScriptObject.Models;

namespace SwaggerToJavaScriptObject.Services;

public class SwaggerAnalyzerService
{
    public AnalyzeResult Analyze(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new AnalyzeResult { Error = "Please enter a Swagger/OpenAPI or RAML." };
        }

        string openApiJson;
        if (input.TrimStart().StartsWith('{'))
        {
            openApiJson = input;
        }
        else if (!TryNormalizeToOpenApiJson(input, out openApiJson))
        {
            return new AnalyzeResult { Error = "Error converting input to OpenAPI JSON. Ensure you provided valid RAML (YAML)." };
        }

        try
        {
            using var doc = JsonDocument.Parse(openApiJson);
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
                Types = [.. types.OrderBy(t => t.Name)],
                OpenApiJson = openApiJson
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

    private static bool TryNormalizeToOpenApiJson(string input, out string result)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.raml");

        try
        {
            File.WriteAllText(tempFilePath, input);
            result = new RamlConverter().Convert(tempFilePath);
            return true;
        }
        catch (Exception)
        {
            result = string.Empty;
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
                // Ignore cleanup failures (e.g., file locked by converter)
            }
        }
    }
}