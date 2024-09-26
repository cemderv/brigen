using brigen.decl;
using System.Diagnostics;

namespace brigen;

public abstract class Decl
{
    protected Decl(string name, CodeRange range)
    {
        Name = name;
        Range = range;
    }

    protected bool IsVerified { get; private set; }
    public Module? Module { get; internal set; }
    public string Name { get; }
    public CommentDecl? Comment { get; set; }
    public AttributeDecl? Attribute { get; set; }
    public CodeRange Range { get; }
    public bool HasAttribute(AttributeKind kind) => Attribute != null && Attribute.Kind == kind;

    public void Verify(Module module)
    {
        if (IsVerified)
            return;

        // Verify that the module cannot be changed to a different one.
        if (Module != null)
            Debug.Assert(module == Module);

        Module = module;

        Comment?.Verify(module);
        Attribute?.Verify(module);

        OnVerify(module);
        IsVerified = true;
    }

    protected abstract void OnVerify(Module module);

    public override string ToString() => GetType().Name + ' ' + Name;
}