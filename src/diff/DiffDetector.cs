using brigen.decl;

namespace brigen.diff;

internal enum DiffDetectorTarget
{
    ImplHeader,
    ImplSource
}

internal sealed class DiffDetector
{
    public const string preambleStartMarker = "// brigen preamble begin";
    public const string preambleEndMarker = "// brigen preamble end";
    private readonly HeaderFileData _headerData;
    private readonly SourceFileData _sourceData;

    public DiffDetector(string implHeaderFilename, string implSourceFilename, Module module)
    {
        Module = module;
        _headerData = new HeaderFileData(this);
        _sourceData = new SourceFileData(this);

        _headerData.Load(implHeaderFilename);
        _sourceData.Load(implSourceFilename);
    }

    public Module Module { get; }

    public FunctionInfo? GetFunctionInfo(DiffDetectorTarget target, FunctionDecl function)
      => GetFunctionInfo(target, target == DiffDetectorTarget.ImplHeader
        ? function.CppSignatureIDStringInImplHeader
        : function.CppSignatureIDStringInImplSource);

    public FunctionInfo? GetFunctionInfo(DiffDetectorTarget target, string functionSignatureIdString)
    {
        if (target == DiffDetectorTarget.ImplHeader)
        {
            foreach (ClassInfo classInfo in _headerData.Classes)
                foreach (FunctionInfo funcInfo in classInfo.Functions)
                    if (funcInfo.SignatureIDString == functionSignatureIdString)
                        return funcInfo;
        }
        else
        {
            foreach (FunctionInfo funcInfo in _sourceData.Functions)
                if (funcInfo.SignatureIDString == functionSignatureIdString)
                    return funcInfo;
        }

        return null;
    }

    public FunctionInfo? GetDtorInfo(DiffDetectorTarget target, ClassDecl clss)
    {
        if (target == DiffDetectorTarget.ImplHeader)
        {
            foreach (ClassInfo classInfo in _headerData.Classes)
                foreach (FunctionInfo funcInfo in classInfo.Functions)
                    if (funcInfo.Kind == FunctionKind.Dtor && funcInfo.SignatureIDString == clss.DtorSignatureIDStringInImplHeader)
                        return funcInfo;
        }
        else
        {
            foreach (FunctionInfo funcInfo in _sourceData.Functions)
                if (funcInfo.Kind == FunctionKind.Dtor && funcInfo.SignatureIDString == clss.DtorSignatureIDStringInImplSource)
                    return funcInfo;
        }

        return null;
    }

    public void GenerateForwardDeclarations(Writer w)
    {
        w.ClearWriteMarker();

        foreach (string fwdDecl in _headerData.ForwardDeclarations)
            w.Write(fwdDecl + ";\n");

        w.WriteNewlineIfWrittenAnythingAndResetMarker();
    }

    public void GenerateImplClassExtraStructs(Writer w, ClassDecl clss)
    {
        string className = clss.Name + "Impl";

        ClassInfo? it = _headerData.Classes.FirstOrDefault(c => c.Name == className);

        if (it == null)
            return;

        if (it.Structs.Count == 0)
            return;

        var currentVis = (Visibility)(-1);

        foreach (StructInfo strct in it.Structs)
        {
            if (strct.Visibility != currentVis)
            {
                // If this is NOT the first visibility change,
                // insert an extra new-line so keep things pretty.
                if (currentVis != 0)
                    w.Write("\n");

                w.Unindent();
                w.Write(GetVisibilityCppString(strct.Visibility));
                w.Write(":\n");
                w.Indent();

                currentVis = strct.Visibility;
            }

            w.Write(strct.Content);
            w.Write(";\n");
        }

        w.Write("\n");
    }

    public void GenerateImplClassExtraFields(Writer w, ClassDecl clss)
    {
        string className = clss.Name + "Impl";

        ClassInfo? it = _headerData.Classes.FirstOrDefault(c => c.Name == className);

        if (it == null)
            return;

        if (it.Fields.Count == 0)
            return;

        w.Write("\n");

        var currentVis = (Visibility)0;

        foreach (FieldInfo field in it.Fields)
        {
            if (field.Visibility != currentVis)
            {
                // If this is NOT the first visibility change,
                // insert an extra new-line so keep things pretty.
                if (currentVis != 0)
                    w.Write("\n");

                w.Unindent();
                w.Write(GetVisibilityCppString(field.Visibility));
                w.Write(":\n");
                w.Indent();

                currentVis = field.Visibility;
            }

            w.Write(field.Content);
            w.Write(";\n");
        }
    }

    public void GenerateImplClassExtraEnums(Writer w, ClassDecl clss)
    {
        string className = clss.Name + "Impl";

        ClassInfo? it = _headerData.Classes.FirstOrDefault(c => c.Name == className);

        if (it == null || it.Enums.Count == 0)
            return;

        w.Write("\n");

        var currentVis = (Visibility)0;

        foreach (EnumInfo enm in it.Enums)
        {
            if (enm.Visibility != currentVis)
            {
                if (currentVis != 0)
                    w.Write("\n");

                w.Unindent();
                w.Write(GetVisibilityCppString(enm.Visibility));
                w.Write(":\n");
                w.Indent();

                currentVis = enm.Visibility;
            }

            w.Write(enm.Content);
            w.Write(";\n");
        }
    }

    public string? HasPreamble(DiffDetectorTarget target)
    {
        string? str = target switch
        {
            DiffDetectorTarget.ImplHeader => _headerData.FileContents,
            DiffDetectorTarget.ImplSource => _sourceData.FileContents,
            _ => null
        };

        if (string.IsNullOrEmpty(str))
            return null;

        int startIdx = str.IndexOf(preambleStartMarker, StringComparison.Ordinal);

        if (startIdx < 0)
            return null;

        int endIdx = str.IndexOf(preambleEndMarker, startIdx, StringComparison.Ordinal);

        if (endIdx < 0)
            return null;

        endIdx += preambleEndMarker.Length;

        return str[startIdx..endIdx];
    }

    public List<FunctionInfo> GetExtraFunctionsForClass(DiffDetectorTarget target, ClassDecl clss)
    {
        var funcs = new List<FunctionInfo>();

        void CheckFunctions(List<FunctionInfo> functions)
        {
            foreach (FunctionInfo funcInfo in functions)
            {
                if (funcInfo.ParentClass != clss)
                    continue;

                // Dtors always exist in a class.
                if (funcInfo.Kind == FunctionKind.Dtor)
                    continue;

                bool exists = false;

                foreach (FunctionDecl funcDecl in clss.AllExportedFunctions)
                {
                    string funcSignatureIdString = target == DiffDetectorTarget.ImplHeader
                      ? funcDecl.CppSignatureIDStringInImplHeader
                      : funcDecl.CppSignatureIDStringInImplSource;

                    if (funcInfo.SignatureIDString == funcSignatureIdString)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    funcs.Add(funcInfo);
            }
        }

        if (target == DiffDetectorTarget.ImplHeader)
        {
            foreach (ClassInfo classInfo in _headerData.Classes)
                CheckFunctions(classInfo.Functions);
        }
        else if (target == DiffDetectorTarget.ImplSource)
        {
            CheckFunctions(_sourceData.Functions);
        }

        return funcs;
    }

    public ClassDecl? FindClassDecl(string namespc, string className)
    {
        if (namespc != Module.Name)
            return null;

        className = className.Replace("Impl", string.Empty);

        TypeDecl? typeDecl = Module.FindTypeDecl(className);

        return typeDecl as ClassDecl;
    }

    public string? GetImplHeaderFileContents() => _headerData.FileContents ?? string.Empty;

    public string? GetImplSourceFileContents() => _sourceData.FileContents ?? string.Empty;

    public static string GetVisibilityCppString(Visibility vis)
      => vis switch
      {
          Visibility.Public => "public",
          Visibility.Protected => "protected",
          Visibility.Private => "private",
          _ => "public"
      };
}