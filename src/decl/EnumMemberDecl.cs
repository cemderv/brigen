using System.Diagnostics;

namespace brigen.decl;

public sealed class EnumMemberDecl : Decl
{
    public EnumMemberDecl(string name, CodeRange range, int? value)
      : base(name, range)
    {
        Value = value;
    }

    public EnumDecl? ParentEnum { get; set; }

    public int? Value { get; private set; }

    protected override void OnVerify(Module module)
    {
        Debug.Assert(ParentEnum != null);

        if (!Value.HasValue)
        {
            EnumMemberDecl? lastMember = null;

            foreach (EnumMemberDecl member in ParentEnum.Members)
            {
                if (member == this)
                    break;
                lastMember = member;
            }

            Value = lastMember?.Value + 1 ?? 0;
        }
    }

    public override string ToString() => Name;
}