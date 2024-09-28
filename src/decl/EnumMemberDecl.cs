using System.Diagnostics;

namespace brigen.decl;

public sealed class EnumMemberDecl(string name, CodeRange range, int? value)
    : Decl(name, range)
{
    public EnumDecl? ParentEnum { get; set; }

    public int? Value { get; private set; } = value;

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