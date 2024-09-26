using brigen.decl;

namespace brigen;

internal enum SpecialCApiFunc
{
    AddRef,
    Release
}

internal enum SpecialCppBuiltInFunction
{
    IsValid = 1,
    GetImpl,
    SetImpl,
    DropImpl
}

internal static class NameResolution
{
    private static readonly Dictionary<(SpecialCApiFunc, CaseStyle), string> _map = new()
  {
    { (SpecialCApiFunc.AddRef, CaseStyle.PascalCase), "AddRef" },
    { (SpecialCApiFunc.AddRef, CaseStyle.CamelCase), "addRef" },
    { (SpecialCApiFunc.Release, CaseStyle.PascalCase), "Release" },
    { (SpecialCApiFunc.Release, CaseStyle.CamelCase), "release" }
  };

    private static readonly Dictionary<(SpecialCppBuiltInFunction, CaseStyle), string> _spmap = new()
  {
    { (SpecialCppBuiltInFunction.IsValid, CaseStyle.PascalCase), "IsValid" },
    { (SpecialCppBuiltInFunction.IsValid, CaseStyle.CamelCase), "isValid" },
    { (SpecialCppBuiltInFunction.GetImpl, CaseStyle.PascalCase), "GetImpl" },
    { (SpecialCppBuiltInFunction.GetImpl, CaseStyle.CamelCase), "getImpl" },
    { (SpecialCppBuiltInFunction.SetImpl, CaseStyle.PascalCase), "SetImpl" },
    { (SpecialCppBuiltInFunction.SetImpl, CaseStyle.CamelCase), "setImpl" },
    { (SpecialCppBuiltInFunction.DropImpl, CaseStyle.PascalCase), "DropImpl" },
    { (SpecialCppBuiltInFunction.DropImpl, CaseStyle.CamelCase), "dropImpl" }
  };

    public static string GetSpecialCApiFuncName(SpecialCApiFunc func, Module module) =>
      !_map.TryGetValue((func, module.CppCaseStyle), out string? str)
        ? throw new ArgumentException("Invalid case style")
        : str;

    public static string GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction func, Module module) =>
      !_spmap.TryGetValue((func, module.CppCaseStyle), out string? str)
        ? throw new ArgumentException("Invalid case style")
        : str;

    // Do not use this directly if you have access to an ClassDecl object.
    // Used by ClassDecl::OnVerify to obtain its impl class name.
    public static string GetCppImplClassName(ClassDecl clss)
      => clss.Name + "Impl";
}