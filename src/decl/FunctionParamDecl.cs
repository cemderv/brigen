using brigen.types;

namespace brigen.decl;

public sealed class FunctionParamDecl : Decl
{
    public FunctionParamDecl(string name, CodeRange range, IDataType type)
      : base(name, range)
    {
        Type = type;
    }

    public string NameInC => string.Empty;
    public Decl? ParentDecl { get; set; }
    public FunctionDecl? ParentFunction => ParentDecl as FunctionDecl;
    public DelegateDecl? ParentDelegate => ParentDecl as DelegateDecl;
    public IDataType Type { get; private set; }
    public int Index { get; set; }
    public int IndexInCApi { get; set; }

    protected override void OnVerify(Module module) => Type = Type.VerifyType(module);
}