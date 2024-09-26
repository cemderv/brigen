using brigen.gen;
using brigen.types;
using System.Text;

namespace brigen.decl;

[Flags]
public enum FunctionFlags
{
    None = 0,
    Ctor = 1,
    Const = 2,
    Static = 4
}

public sealed class FunctionDecl : Decl
{
    private readonly List<FunctionParamDecl> _parameters;
    private TypeDecl? _parentTypeDecl;

    public FunctionDecl(string name, CodeRange range, FunctionFlags flags, IDataType returnType,
      List<FunctionParamDecl> parameters)
      : base(name, range)
    {
        Flags = flags;
        ReturnType = returnType;
        _parameters = parameters;

        foreach (FunctionParamDecl param in _parameters)
            param.ParentDecl = this;

        FunctionHelper.DetermineFuncParamIndices(_parameters);
    }

    public bool IsDefaultCtorOfItsClass
    {
        get
        {
            ClassDecl? clss = ParentAsClass;
            if (clss == null)
                return false;

            return _parameters.Count == 0 && Name == clss.Name;
        }
    }

    public TypeDecl? ParentTypeDecl
    {
        get => _parentTypeDecl;
        set
        {
            _parentTypeDecl = value;

            if (_parentTypeDecl is ClassDecl { IsStatic: true })
            {
                Flags |= FunctionFlags.Static;
                Flags &= ~FunctionFlags.Const;
            }
        }
    }

    public ClassDecl? ParentAsClass => ParentTypeDecl as ClassDecl;

    public IDataType ReturnType { get; private set; }

    public bool HasThisParam
    {
        get
        {
            if (IsStatic)
                return false;

            return !IsCtor;
        }
    }

    public bool HasNonVoidReturnType
      => ReturnType != PrimitiveType.Void;

    public bool HasOutReturnParam
    {
        get
        {
            if (IsCtor)
                return false;

            if (!HasNonVoidReturnType)
                return false;

            return ReturnType.IsStruct || ReturnType.IsArray;
        }
    }

    public string NameInC { get; private set; } = string.Empty;

    public string NameInCSharp { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the name of the corresponding method in the .java file.
    /// </summary>
    public string NameInJava { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the name of the corresponding <b>native</b> method in the .java file.
    /// </summary>
    public string NativeNameInJava { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the name of the corresponding JNI method in the .cpp file.
    /// </summary>
    public string NativeNameInJNI { get; private set; } = string.Empty;

    public FunctionFlags Flags { get; private set; }

    public bool IsConst => (Flags & FunctionFlags.Const) == FunctionFlags.Const;

    public bool IsCtor => (Flags & FunctionFlags.Ctor) == FunctionFlags.Ctor;

    public bool IsStatic => (Flags & FunctionFlags.Static) == FunctionFlags.Static;

    public IReadOnlyList<FunctionParamDecl> Parameters => _parameters;

    public IEnumerable<FunctionParamDecl> DelegateParameters
      => _parameters.Where(p => p.Type.IsDelegate);

    public string CppSignatureIDStringInImplHeader { get; private set; } = string.Empty;

    public string CppSignatureIDStringInImplSource { get; private set; } = string.Empty;

    // If this function was created from a PropertyDecl, this is the PropertyDecl that caused its creation.
    public PropertyDecl? OriginalProperty { get; init; }

    protected override void OnVerify(Module module)
    {
        NameInC = $"{module.Name}_{ParentTypeDecl!.Name}_{Name}";
        NameInCSharp = Name.Cased(CaseStyle.PascalCase);
        NameInJava = Name.Cased(CaseStyle.CamelCase);
        NativeNameInJava = NameInJava + "Native";

        NativeNameInJNI = $"Java_{ParentTypeDecl.QualifiedNameInJava}_{NativeNameInJava}".Replace('.', '_');

        ReturnType = ReturnType.VerifyType(module);

        foreach (var parameter in _parameters)
            parameter.Verify(module);

        if (!IsConst && Strings.ConstMakerPrefixes.Any(prefix => Name.StartsWith(prefix)))
            Flags |= FunctionFlags.Const;

        if (IsStatic)
            Flags &= ~FunctionFlags.Const;

        CppSignatureIDStringInImplHeader =
          ComputeSignatureIDString(CppCodeGenerator.GetFunctionSignature(this, true, true));
        CppSignatureIDStringInImplSource =
          ComputeSignatureIDString(CppCodeGenerator.GetFunctionSignature(this, false, true));
    }

    public static string ComputeSignatureIDString(string signature)
      => signature
        .Replace("\n", string.Empty)
        .Replace(" ", string.Empty);

    public override string ToString()
    {
        var sb = new StringBuilder(64);

        sb.Append(ReturnType)
          .Append(' ')
          .Append(Name)
          .Append('(');

        if (_parameters.Count == 1)
            sb.Append(_parameters[0].Name);
        else if (_parameters.Count > 1)
            sb.Append("...");

        sb.Append(')');

        return sb.ToString();
    }
}