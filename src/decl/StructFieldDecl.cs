using brigen.types;
using System.Diagnostics;

namespace brigen.decl;

public sealed class StructFieldDecl(string name, CodeRange range, IDataType type)
    : Decl(name, range)
{
    public IDataType Type { get; private set; } = type;
    public bool AppearsInCtors { get; } = true;
    public string NameInCpp { get; private set; } = string.Empty;
    public string NameInJava { get; private set; } = string.Empty;

    protected override void OnVerify(Module module)
    {
        Type = Type.VerifyType(module);

        NameInCpp = Name.Cased(module.CppCaseStyle);
        Debug.Assert(!string.IsNullOrEmpty(NameInCpp));

        NameInJava = Name.CamelCased();
    }
}