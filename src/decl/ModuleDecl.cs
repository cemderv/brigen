namespace brigen.decl;

public class ModuleDecl(string name, CodeRange range)
    : Decl(name, range)
{
    protected override void OnVerify(Module module)
    {
    }

    public override string ToString() => $"module {Name}";
}