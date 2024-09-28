namespace brigen.decl;

public sealed class StructDecl(string name, CodeRange range, List<StructFieldDecl> fields) : TypeDecl(name, range)
{
    private readonly List<StructFieldDecl> _fields = fields;

    public IReadOnlyList<StructFieldDecl> Fields => _fields;

    public IEnumerable<StructFieldDecl> FieldsForCtors => _fields.Where(f => f.AppearsInCtors);

    public override bool IsStruct => true;

    protected override void OnVerify(Module module)
    {
        base.OnVerify(module);

        if (_fields.Count == 0)
            throw new CompileError($"Struct '{Name}' does not declare any fields", Range);

        foreach (StructFieldDecl field in _fields)
        {
            if (field.Type.Name == Name)
                throw new CompileError("A struct cannot contain a field of its own type", field.Range);

            field.Verify(module);
        }
    }

    public override string ToString() => $"{Name} : struct";
}