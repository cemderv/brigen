using brigen.decl;
using brigen.types;

namespace brigen;

public sealed class Module
{
    private readonly List<Decl> _decls;
    private readonly Dictionary<string, object> _variables;
    private readonly List<ImportDecl> _importDecls;
    private readonly List<IDataType> _dataTypes;
    private readonly List<ClassDecl> _allClasses;
    private readonly List<DelegateDecl> _allDelegates;
    private readonly List<EnumDecl> _allEnums;
    private readonly List<FunctionDecl> _allExportedFunctions;
    private readonly List<StructDecl> _allStructs;
    private readonly List<TypeDecl> _allTypeDecls;
    private readonly List<TypeDecl> _allTypeDeclsWithoutEnums;

    public Module(Settings settings, List<Decl> decls)
    {
        Settings = settings;
        _decls = decls;

        foreach (var decl in _decls)
            decl.Module = this;

        ProcessHeaderDecls();

        _importDecls = _decls.OfType<ImportDecl>().ToList();
        foreach (var importDecl in _importDecls)
            importDecl.Verify(this);

        _variables = new Dictionary<string, object>
    {
      { VarNames.CSharpLibName, $"{Name}NET" },
      { VarNames.PythonLibName, $"py{Name}" },
      { VarNames.JavaLibName, $"j{Name}" },
    };

        ExtractVariables();

        DllExportApi = UpperName + "_API";
        CallConvApi = UpperName + "_CALLCONV";
        CppCallConvValue = "cdecl";
        JavaPackageName = string.IsNullOrEmpty(CompanyId) ? $"com.{Name}" : $"com.{CompanyId}.{Name}";

        Paths = new Paths(this);

        ThisParam = new FunctionParamDecl("this", default, PrimitiveType.Void);
        ThisParam.Verify(this);

        _dataTypes = _decls.OfType<IDataType>().ToList();

        new ModuleVerifier().VerifyModule(this);

        {
            _allEnums = _decls.OfType<EnumDecl>().ToList();
            _allStructs = _decls.OfType<StructDecl>().ToList();
            _allClasses = _decls.OfType<ClassDecl>().ToList();
            _allDelegates = _decls.OfType<DelegateDecl>().ToList();
            _allExportedFunctions = new List<FunctionDecl>(_allClasses.SelectMany(i => i.AllExportedFunctions));
            _allTypeDecls = _decls.OfType<TypeDecl>().ToList();
            _allTypeDeclsWithoutEnums = _allTypeDecls.Where(t => t is not EnumDecl).ToList();
        }
    }

    private void ProcessHeaderDecls()
    {
        if (_decls[0] is not ModuleDecl moduleDecl)
        {
            throw new CompileError("The first declaration must be a module declaration. Example: 'module myLib'",
              _decls[0].Range, ErrorCategory.General);
        }

        moduleDecl.Verify(this);

        Name = moduleDecl.Name;

        // Verify that these imports are the only imports.
        // Something like this should not be allowed:
        // <importdecl>
        // <importdecl>
        // <someotherdecl>
        // <importdecl>     <-- Not allowed. All imports should be at the top.
        int idxOfFirstNonImportDecl = -1;
        for (int i = 1; i < _decls.Count; ++i)
        {
            var decl = _decls[i];
            if (decl is not ImportDecl)
            {
                idxOfFirstNonImportDecl = i;
                break;
            }
        }

        for (int i = idxOfFirstNonImportDecl + 1; i < _decls.Count; ++i)
        {
            var decl = _decls[i];
            if (decl is ImportDecl importDecl)
            {
                throw new CompileError(
                  "Import declarations must be stated before other types of declarations, but after module declarations.",
                  importDecl.Range, ErrorCategory.General);
            }
        }
    }

    private void ExtractVariables()
    {
        foreach (var setVariableDecl in _decls.OfType<SetVariableDecl>())
        {
            setVariableDecl.Verify(this);

            if (_variables.ContainsKey(setVariableDecl.Name))
                _variables[setVariableDecl.Name] = setVariableDecl.VariableValue;
            else
                _variables.Add(setVariableDecl.Name, setVariableDecl.VariableValue);
        }
    }

    public bool TryGetVariable(string variableName, out object? value)
      => _variables.TryGetValue(variableName, out value);

    public string GetStringVariable(string variableName, string defaultValue)
      => TryGetVariable(variableName, out object? value) && value is string str ? str : defaultValue;

    public bool GetBoolVariable(string variableName, bool defaultValue)
      => TryGetVariable(variableName, out object? value) && value is bool b ? b : defaultValue;

    public int GetIntVariable(string variableName, int defaultValue)
      => TryGetVariable(variableName, out object? value) && value is int i ? i : defaultValue;

    public string Filename => Settings.InputFilename;

    public string Name { get; private set; } = string.Empty;
    public string UpperName => Name.ToUpperInvariant();
    public string CompanyId => GetStringVariable(VarNames.CompanyId, string.Empty);
    public string Company => GetStringVariable(VarNames.Company, string.Empty);
    public string Description => GetStringVariable(VarNames.Description, string.Empty);
    public ModuleVersion Version { get; private set; } = new(1, 0, 0, string.Empty);
    public bool EnableClangFormat => GetBoolVariable(VarNames.EnableClangFormat, false);
    public string ClangFormatLocation => GetStringVariable(VarNames.ClangFormatLocation, string.Empty);
    public CaseStyle CppCaseStyle { get; private set; } = CaseStyle.PascalCase;
    public bool CppVectorSupport => GetBoolVariable(VarNames.CppVectorSupport, true);
    public bool CppGenStdHash => GetBoolVariable(VarNames.CppGenStdHash, true);
    public int HashFirstPrime => GetIntVariable(VarNames.HashFirstPrime, 17);
    public int HashSecondPrime => GetIntVariable(VarNames.HashSecondPrime, 23);
    public string CSharpLibName => GetStringVariable(VarNames.CSharpLibName, "");
    public string PythonLibName => GetStringVariable(VarNames.PythonLibName, "");
    public string JavaLibName => GetStringVariable(VarNames.JavaLibName, "");

    public string DllExportApi { get; }
    public string CallConvApi { get; }
    public string CppCallConvValue { get; }
    public string JavaPackageName { get; }
    public string JavaPackageNameForwardSlashed => JavaPackageName.Replace('.', '/');

    public IReadOnlyList<Decl> Decls => _decls;
    public Settings Settings { get; }
    public Paths Paths { get; }
    public FunctionParamDecl ThisParam { get; }

    public IReadOnlyList<FunctionDecl> AllExportedFunctions => _allExportedFunctions;
    public IReadOnlyList<EnumDecl> AllEnums => _allEnums;
    public IReadOnlyList<StructDecl> AllStructs => _allStructs;
    public IReadOnlyList<ClassDecl> AllClasses => _allClasses;
    public IReadOnlyList<DelegateDecl> AllDelegates => _allDelegates;

    public IEnumerable<TypeDecl> AllTypeDecls(bool withEnums) => withEnums ? _allTypeDecls : _allTypeDeclsWithoutEnums;

    public IDataType? FindType(string name)
    {
        IDataType? ret = _dataTypes.Find(d => d.Name == name);

        ret ??= PrimitiveType.Get(name);

        return ret;
    }

    public TypeDecl? FindTypeDecl(string name)
      => _dataTypes.OfType<TypeDecl>().FirstOrDefault(t => t.Name == name);

    public Decl? FindSimilarlyNamedDecl(string name)
    {
        static bool AreStringsSimilar(string lhs, string rhs)
        {
            return StringExtensions.LevenshteinDistanceNormalized(lhs, rhs) < 0.5f;
        }

        return _decls.FirstOrDefault(d => AreStringsSimilar(d.Name, name));
    }

    public override string ToString() => $"Module '{Path.GetFileName(Filename)}'";
}