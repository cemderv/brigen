using brigen.types;
using System.Diagnostics;
using System.Text;

namespace brigen.decl;

public sealed class PropertyDecl : Decl
{
    [Flags]
    public enum PropMask
    {
        Getter = 1,
        Setter = 2
    }

    private TypeDecl? _parentTypeDecl;

    public PropertyDecl(string name, CodeRange range, PropMask mask, IDataType type)
      : base(name, range)
    {
        Mask = mask;
        Type = type;
    }

    public TypeDecl? ParentTypeDecl
    {
        get => _parentTypeDecl;
        set
        {
            Debug.Assert(!IsVerified);
            _parentTypeDecl = value;

            if (value is ClassDecl { IsStatic: true })
                IsStatic = true;
        }
    }

    public ClassDecl? ParentAsClass => ParentTypeDecl as ClassDecl;
    public PropMask Mask { get; }
    public bool HasGetter => (Mask & PropMask.Getter) == PropMask.Getter;
    public bool HasSetter => (Mask & PropMask.Setter) == PropMask.Setter;
    public IDataType Type { get; private set; }

    public bool IsStatic { get; private set; }

    public string GetterNameInCpp { get; private set; } = string.Empty;

    public string SetterNameInCpp { get; private set; } = string.Empty;

    protected override void OnVerify(Module module)
    {
        Type = Type.VerifyType(module);

        if (HasGetter)
        {
            GetterNameInCpp = module.CppCaseStyle switch
            {
                CaseStyle.PascalCase => "Get",
                CaseStyle.CamelCase => "get",
                _ => string.Empty
            };
            GetterNameInCpp += Name;
        }

        if (HasSetter)
        {
            SetterNameInCpp = module.CppCaseStyle switch
            {
                CaseStyle.PascalCase => "Set",
                CaseStyle.CamelCase => "set",
                _ => string.Empty
            };
            SetterNameInCpp += Name;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder(64);

        if (HasGetter)
            sb.Append("get ");

        if (HasSetter)
            sb.Append("set ");

        sb.Append(Name);

        return sb.ToString();
    }
}