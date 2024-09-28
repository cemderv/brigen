using brigen.types;

namespace brigen.decl;

public abstract class TypeDecl(string name, CodeRange range)
    : Decl(name, range), IDataType
{
    public string CppName => Name;
    public string NameInJava => Name;
    public string QualifiedNameInJava => Module != null ? $"{Module.JavaPackageName}.{NameInJava}" : string.Empty;
    public string NameInJavaForFindClass => QualifiedNameInJava.Replace('.', '/');

    public virtual bool IsClass => false;
    public bool IsArray => false;
    public virtual bool IsStruct => false;
    public virtual bool IsDelegate => false;
    public virtual bool IsEnum => false;
    public bool IsUserDefined => true;

    public IDataType VerifyType(Module module)
    {
        Verify(module);
        return this;
    }

    protected override void OnVerify(Module module)
    {
        if (Name.StartsWith(Strings.ForbiddenIdentifierPrefix))
            throw new CompileError(
              $"Declaration \"{Name}\" has an invalid name. The prefix \"{Strings.ForbiddenIdentifierPrefix}\" is reserved for special identifiers.",
              Range);
    }
}