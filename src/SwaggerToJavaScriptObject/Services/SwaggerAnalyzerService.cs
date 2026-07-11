using System.Text.Json;
using System.Text.Json.Nodes;
using RamlToOpenApiConverter;
using SwaggerToJavaScriptObject.Models;

namespace SwaggerToJavaScriptObject.Services;

public class SwaggerAnalyzerService
{
    public AnalyzeResult Analyze(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new AnalyzeResult { Error = "Please enter a full Swagger/OpenAPI JSON document or a RAML YAML document." };
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

            // Extract inline path response schemas and inject into the named schemas section
            var (modifiedJson, pathTypeNames) = InjectPathResponseSchemas(openApiJson);
            openApiJson = modifiedJson;
            types.AddRange(pathTypeNames.Select(n => new TypeDefinition { Name = n }));

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

    private static (string ModifiedJson, List<string> AddedTypeNames) InjectPathResponseSchemas(string openApiJson)
    {
        var root = JsonNode.Parse(openApiJson)!.AsObject();
        var addedNames = new List<string>();

        if (root["paths"] is not JsonObject paths)
        {
            return (openApiJson, addedNames);
        }

        // Ensure components/schemas node exists
        if (root["components"] is not JsonObject components)
        {
            components = new JsonObject();
            root["components"] = components;
        }

        if (components["schemas"] is not JsonObject schemas)
        {
            schemas = new JsonObject();
            components["schemas"] = schemas;
        }

        foreach (var pathEntry in paths)
        {
            if (pathEntry.Value is not JsonObject pathItem)
            {
                continue;
            }

            // Build type-name prefix: "/pet/findByStatus" -> "pet_findByStatus"
            var pathName = pathEntry.Key
                .TrimStart('/')
                .Replace('/', '_')
                .Replace("{", string.Empty)
                .Replace("}", string.Empty);

            foreach (var methodEntry in pathItem)
            {
                if (methodEntry.Value is not JsonObject operation)
                {
                    continue;
                }

                if (operation["responses"] is not JsonObject responses)
                {
                    continue;
                }

                foreach (var responseEntry in responses)
                {
                    if (responseEntry.Key == "default")
                    {
                        continue;
                    }

                    if (responseEntry.Value is not JsonObject response)
                    {
                        continue;
                    }

                    if (response["content"] is not JsonObject content)
                    {
                        continue;
                    }

                    if (content["application/json"] is not JsonObject jsonContent)
                    {
                        continue;
                    }

                    if (jsonContent["schema"] is not JsonObject schema)
                    {
                        continue;
                    }

                    // Skip pure $ref schemas — already covered by named schemas
                    if (schema.ContainsKey("$ref"))
                    {
                        continue;
                    }

                    var typeName = $"{pathName}_{responseEntry.Key}";

                    // Skip if already present (e.g. multiple methods sharing the same path+status)
                    if (schemas.ContainsKey(typeName))
                    {
                        continue;
                    }

                    // Clone the schema node (JsonNode cannot have two parents)
                    schemas[typeName] = JsonNode.Parse(schema.ToJsonString());
                    addedNames.Add(typeName);
                }
            }
        }

        if (addedNames.Count == 0)
        {
            return (openApiJson, addedNames);
        }

        return (root.ToJsonString(), addedNames);
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