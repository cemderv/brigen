namespace brigen.decl;

public class SetVariableDecl(string variableName, CodeRange range, object variableValue)
    : Decl(variableName, range)
{
    private static readonly Dictionary<string, Type> _varTypeTable = new()
  {
    { VariableNames.CompanyId, typeof(string) },
    { VariableNames.Company, typeof(string) },
    { VariableNames.Description, typeof(string) },
    { VariableNames.Version, typeof(string) },
    { VariableNames.EnableClangFormat, typeof(bool) },
    { VariableNames.ClangFormatLocation, typeof(string) },
    { VariableNames.NativePublicDir, typeof(string) },
    { VariableNames.NativePrivateDir, typeof(string) },
    { VariableNames.CppCaseStyle, typeof(string) },
    { VariableNames.CppVectorSupport, typeof(bool) },
    { VariableNames.CppGenStdHash, typeof(bool) },
    { VariableNames.HashFirstPrime, typeof(int) },
    { VariableNames.HashSecondPrime, typeof(int) },
    { VariableNames.CSharpOutDir, typeof(string) },
    { VariableNames.CSharpLibName, typeof(string) },
    { VariableNames.CSharpNullRef, typeof(bool) },
    { VariableNames.PythonCppFile, typeof(string) },
    { VariableNames.PythonLibName, typeof(string) },
    { VariableNames.JavaOutDir, typeof(string) },
    { VariableNames.JavaLibName, typeof(string) },
  };

    protected override void OnVerify(Module module)
    {
        if (!_varTypeTable.TryGetValue(Name, out Type? type))
        {
            throw new CompileError($"Attempting to set unknown variable '{Name}'.", Range);
        }

        if (VariableValue.GetType() != type)
        {
            throw new CompileError(
              $"Variable '{Name}' is of type '{type.Name}', but attempting to assign a value of type '{VariableValue.GetType().Name}' to it.",
              Range);
        }
    }

    public object VariableValue { get; } = variableValue;
}