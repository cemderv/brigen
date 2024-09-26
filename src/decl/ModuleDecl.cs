namespace brigen.decl;

public class ModuleDecl : Decl
{
    public ModuleDecl(string name, CodeRange range)
      : base(name, range)
    {
    }

    protected override void OnVerify(Module module)
    {
    }

    public override string ToString() => $"module {Name}";
}