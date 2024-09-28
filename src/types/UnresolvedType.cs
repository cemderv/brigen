using System.Diagnostics;

namespace brigen.types;

public sealed class UnresolvedType : IDataType
{
    private static readonly Dictionary<string, UnresolvedType> s_TypeMap = [];
    private bool _isVerified;
    private IDataType _resolvedType;

    private UnresolvedType(string name)
    {
        Name = name;
        _resolvedType = PrimitiveType.Undefined;
    }

    public IDataType VerifyType(Module scope)
    {
        if (_isVerified)
        {
            Debug.Assert(_resolvedType != PrimitiveType.Undefined);
            return _resolvedType;
        }

        IDataType? resolvedType = scope.FindType(Name);

        _resolvedType = resolvedType ?? throw CompileError.UndefinedSymbolUsed(scope, Name, default);

        _isVerified = true;

        return _resolvedType;
    }

    public string Name { get; }
    public string NameInC => Name;
    public string NameInCpp => Name;
    public bool IsClass => false;
    public bool IsArray => false;
    public bool IsStruct => false;
    public bool IsDelegate => false;
    public bool IsEnum => false;
    public bool IsUserDefined => false;

    public static UnresolvedType Get(string name)
    {
        if (!s_TypeMap.TryGetValue(name, out UnresolvedType? unresolvedType))
        {
            unresolvedType = new UnresolvedType(name);
            s_TypeMap.Add(name, unresolvedType);
        }

        return unresolvedType;
    }

    public override string ToString() => $"UnresolvedType '{Name}'";
}