namespace SwaggerToJavaScriptObject.Models;

public class AnalyzeResult
{
    public List<TypeDefinition> Types { get; set; } = [];

    public string OpenApiJson { get; set; } = string.Empty;

    public string? Error { get; set; }
}