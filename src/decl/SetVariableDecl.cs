namespace brigen.decl;

public class SetVariableDecl : Decl
{
    private static readonly Dictionary<string, Type> _varTypeTable = new()
  {
    { VarNames.CompanyId, typeof(string) },
    { VarNames.Company, typeof(string) },
    { VarNames.Description, typeof(string) },
    { VarNames.Version, typeof(string) },
    { VarNames.EnableClangFormat, typeof(bool) },
    { VarNames.ClangFormatLocation, typeof(string) },
    { VarNames.NativePublicDir, typeof(string) },
    { VarNames.NativePrivateDir, typeof(string) },
    { VarNames.CppCaseStyle, typeof(string) },
    { VarNames.CppVectorSupport, typeof(bool) },
    { VarNames.CppGenStdHash, typeof(bool) },
    { VarNames.HashFirstPrime, typeof(int) },
    { VarNames.HashSecondPrime, typeof(int) },
    { VarNames.CSharpOutDir, typeof(string) },
    { VarNames.CSharpLibName, typeof(string) },
    { VarNames.PythonCppFile, typeof(string) },
    { VarNames.PythonLibName, typeof(string) },
    { VarNames.JavaOutDir, typeof(string) },
    { VarNames.JavaLibName, typeof(string) },
  };

    public SetVariableDecl(string variableName, CodeRange range, object variableValue)
      : base(variableName, range)
    {
        VariableValue = variableValue;
    }

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

    public object VariableValue { get; }
}