using brigen.types;

namespace brigen.decl;

public sealed class DelegateDecl : TypeDecl
{
    private readonly List<FunctionParamDecl> _parameters;

    public DelegateDecl(string name, CodeRange range, List<FunctionParamDecl> parameters,
      IDataType returnType)
      : base(name, range)
    {
        _parameters = parameters;
        _parameters.ForEach(p => p.ParentDecl = this);

        ReturnType = returnType;

        FunctionHelper.DetermineFuncParamIndices(_parameters);
    }

    public IDataType ReturnType { get; private set; }

    public IReadOnlyList<FunctionParamDecl> Parameters => _parameters;

    public override bool IsDelegate => true;

    protected override void OnVerify(Module module)
    {
        base.OnVerify(module);
        ReturnType = ReturnType.VerifyType(module);
        _parameters.ForEach(p => p.Verify(module));
    }

    public override string ToString() => $"delegate {Name}";
}