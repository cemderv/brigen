namespace brigen;

public sealed class Settings
{
    public string InputFilename = string.Empty;
    public string OutputDirectory = string.Empty;
    public bool GenerateCSharpBindings;
    public bool GeneratePythonBindings;
    public bool GenerateJavaBindings;
}