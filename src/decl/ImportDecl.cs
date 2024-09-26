namespace brigen.decl;

public class ImportDecl : Decl
{
    public ImportDecl(string moduleName, CodeRange range)
      : base(moduleName, range)
    {
    }

    protected override void OnVerify(Module module)
    {
        string absoluteFilename = Path.IsPathFullyQualified(Name) ? Name : Path.Combine(module.Filename, Name);

        if (!File.Exists(absoluteFilename))
            throw new CompileError($"'{Name}': no such file");

        AbsoluteFilename = absoluteFilename.CleanPath();
    }

    public string AbsoluteFilename { get; private set; } = string.Empty;

    public override string ToString() => $"import \"{Name}\"";
}