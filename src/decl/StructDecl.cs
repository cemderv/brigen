using brigen.Properties;

namespace brigen.decl;

public sealed class StructDecl : TypeDecl
{
    private readonly List<StructFieldDecl> _fields;

    public StructDecl(string name, CodeRange range, List<StructFieldDecl> fields)
      : base(name, range)
    {
        _fields = fields;
    }

    public IReadOnlyList<StructFieldDecl> Fields => _fields;

    public IEnumerable<StructFieldDecl> FieldsForCtors => _fields.Where(f => f.AppearsInCtors);

    public override bool IsStruct => true;

    protected override void OnVerify(Module module)
    {
        base.OnVerify(module);

        if (_fields.Count == 0)
            throw new CompileError(string.Format(Messages.StructNoFields, Name), Range);

        foreach (StructFieldDecl field in _fields)
        {
            if (field.Type.Name == Name)
                throw new CompileError(Messages.StructNoFieldOfOwnType, field.Range);

            field.Verify(module);
        }
    }

    public override string ToString() => $"{Name} : struct";
}