using brigen.types;
using System.Diagnostics;
using System.Text;

namespace brigen.decl;

public sealed class ClassDecl : TypeDecl
{
    [Flags]
    public enum Modifier
    {
        None = 0,
        Static = 1
    }

    private readonly List<FunctionDecl> _ctors;
    private readonly List<FunctionDecl> _functions;
    private readonly List<PropertyDecl> _properties;
    private readonly List<FunctionDecl> _allExportedFunctions;
    private readonly List<FunctionDecl> _allExportedFunctionsWithoutCtors;
    private List<PropToFuncKey> _propToFuncKeys = new();

    public ClassDecl(string name, CodeRange range, List<FunctionDecl> functions, List<PropertyDecl> properties,
      Modifier modifiers)
      : base(name, range)
    {
        _properties = properties;
        Modifiers = modifiers;

        _ctors = functions.Where(f => f.IsCtor).ToList();
        _functions = functions.Where(f => !f.IsCtor).ToList();

        _allExportedFunctions = new List<FunctionDecl>();
        _allExportedFunctionsWithoutCtors = new List<FunctionDecl>();

        foreach (var ctor in _ctors)
            ctor.ParentTypeDecl = this;

        foreach (var func in _functions)
            func.ParentTypeDecl = this;

        foreach (var prop in _properties)
            prop.ParentTypeDecl = this;
    }

    public IReadOnlyList<FunctionDecl> Ctors => _ctors;

    public IReadOnlyList<FunctionDecl> Functions => _functions;

    public IReadOnlyList<PropertyDecl> Properties => _properties;

    public IReadOnlyList<FunctionDecl> AllExportedFunctions => _allExportedFunctions;

    public IReadOnlyList<FunctionDecl> AllExportedFunctionsWithoutCtors => _allExportedFunctionsWithoutCtors;

    public Modifier Modifiers { get; }

    public bool IsStatic => HasModifier(Modifier.Static);

    public string DtorSignatureIDStringInImplHeader { get; private set; } = string.Empty;

    public string DtorSignatureIDStringInImplSource { get; private set; } = string.Empty;

    public bool IsDisposable => !IsStatic;

    public string ImplClassName { get; private set; } = string.Empty;

    public string QualifiedImplClassName { get; private set; } = string.Empty;

    public bool IsAbstractImpl => Attribute is { Kind: AttributeKind.AbstractImpl };

    public override bool IsClass => true;

    private void GatherAllExportedFunctions()
    {
        var modl = Module;
        Debug.Assert(modl != null);

        _allExportedFunctions.AddRange(_ctors);
        _allExportedFunctions.AddRange(_functions);

        _propToFuncKeys = new List<PropToFuncKey>();

        foreach (PropertyDecl prop in _properties)
        {
            if (prop.HasGetter)
            {
                var func = new FunctionDecl(prop.GetterNameInCpp, prop.Range, FunctionFlags.Const, prop.Type,
                  new List<FunctionParamDecl>())
                { ParentTypeDecl = this, Attribute = prop.Attribute, OriginalProperty = prop };

                func.Verify(modl);

                _propToFuncKeys.Add(new PropToFuncKey(prop, PropertyDecl.PropMask.Getter, func));
                _allExportedFunctions.Add(func);
            }

            if (prop.HasSetter)
            {
                var pars = new List<FunctionParamDecl> { new("value", prop.Range, prop.Type) };

                var func =
                  new FunctionDecl(prop.SetterNameInCpp, prop.Range, FunctionFlags.None, PrimitiveType.Void, pars)
                  {
                      ParentTypeDecl = this,
                      OriginalProperty = prop,
                      Attribute = prop.Attribute
                  };

                func.Verify(modl);

                _propToFuncKeys.Add(new PropToFuncKey(prop, PropertyDecl.PropMask.Setter, func));

                _allExportedFunctions.Add(func);
            }
        }

        _allExportedFunctionsWithoutCtors.AddRange(_allExportedFunctions.Where(f => !f.IsCtor));
    }

    protected override void OnVerify(Module module)
    {
        base.OnVerify(module);

        var modl = Module;
        Debug.Assert(modl != null);

        ImplClassName = NameResolution.GetCppImplClassName(this);
        QualifiedImplClassName = $"{modl.Name}::{ImplClassName}";
        DtorSignatureIDStringInImplHeader = FunctionDecl.ComputeSignatureIDString($"~{ImplClassName}()noexceptoverride");
        DtorSignatureIDStringInImplSource =
          FunctionDecl.ComputeSignatureIDString($"{ImplClassName}::~{ImplClassName}()noexcept");

        if (string.IsNullOrEmpty(DtorSignatureIDStringInImplHeader))
            throw new CompileError($"Class {Name}: Invalid dtor signature in header", Range);

        if (string.IsNullOrEmpty(DtorSignatureIDStringInImplSource))
            throw new CompileError($"Class {Name}: Invalid dtor signature in source", Range);

        _ctors.ForEach(c => c.Verify(modl));
        _functions.ForEach(f => f.Verify(modl));
        _properties.ForEach(p => p.Verify(modl));

        GatherAllExportedFunctions();
    }

    public FunctionDecl? GetFunctionForProperty(PropertyDecl prop, PropertyDecl.PropMask mask)
      => _propToFuncKeys.FirstOrDefault(k => k.Prop == prop && k.Mask == mask)?.Func;

    public bool HasModifier(Modifier value) => (Modifiers & value) == value;

#if DEBUG
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append(Name);
        sb.Append(" : ");

        if (IsStatic)
            sb.Append("static ");

        sb.Append("class ");

        return sb.ToString();
    }
#endif

    private record PropToFuncKey(PropertyDecl Prop, PropertyDecl.PropMask Mask, FunctionDecl Func);
}