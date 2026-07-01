namespace SwaggerToJavaScriptObject.Models;

public class AnalyzeResult
{
    public List<TypeDefinition> Types { get; set; } = [];

    public string? Error { get; set; }
}