namespace brigen;

internal static class Strings
{
    public const string KwEnum = "enum";
    public const string KwStruct = "struct";
    public const string KwClass = "class";
    public const string KwStatic = "static";
    public const string KwDelegate = "delegate";
    public const string KwFunc = "func";
    public const string KwCtor = "ctor";
    public const string KwGet = "get";
    public const string KwSet = "set";
    public const string KwArray = "array";
    public const string KwConst = "const";
    public const string KwModule = "module";
    public const string KwImport = "import";
    public const string KwTrue = "true";
    public const string KwFalse = "false";

    public const string CSharpPInvokeThisParamName = "@this";
    public const string CSharpPInvokeResultArrayName = "brigen_resultArray";
    public const string CSharpPInvokeResultArraySizeName = "brigen_resultArraySize";
    public const string CSharpPInvokeResultName = "brigen_result";
    public const string CppImplCtorName = "CreateImpl";
    public const string CApiThisParam = "this_";
    public const string JniThisParam = "this_";

    public const string ForbiddenIdentifierPrefix = "brigen_";
    public const string CppInternalNamespace = "brigen_internal";
    public const string CppBool32TypeName = "bool32_t";

    /// <summary>
    ///   If a function has any of these prefixes, it counts as const.
    /// </summary>
    public static readonly string[] ConstMakerPrefixes =
    {
    "get", "Get", "is", "Is", "has", "Has", "contains", "Contains"
  };
}