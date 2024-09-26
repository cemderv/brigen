namespace brigen.decl;

public sealed class EnumDecl : TypeDecl
{
    private readonly List<EnumMemberDecl> _members;

    public EnumDecl(string name, CodeRange range, List<EnumMemberDecl> members)
      : base(name, range)
    {
        _members = members;
        _members.ForEach(m => m.ParentEnum = this);
        IsFlags = Name.Contains("Flags");
    }

    public bool IsFlags { get; }

    public override bool IsEnum => true;

    public IReadOnlyList<EnumMemberDecl> Members => _members;

    protected override void OnVerify(Module module)
    {
        base.OnVerify(module);
        _members.ForEach(m => m.Verify(module));
    }

    public override string ToString() => $"{Name} : enum";
}