using System.Diagnostics;

namespace brigen;

public sealed class Paths
{
    public const string CppBool32HeaderName = "bool32_t.hpp";

    public Paths(Module module)
    {
        Debug.Assert(!string.IsNullOrEmpty(module.Name));

        Settings settings = module.Settings;

        OutputDir = settings.OutputDirectory.CleanPath();

        string specifiedNativePublicDir = module.GetStringVariable(VarNames.NativePublicDir, "include");
        string nativePublicDir = Path.Combine(OutputDir, specifiedNativePublicDir).CleanPath();

        string specifiedNativePrivateDir = module.GetStringVariable(VarNames.NativePrivateDir, "src");
        string nativePrivateDir = Path.Combine(OutputDir, specifiedNativePrivateDir).CleanPath();

        CppHelpersHeader = Path.Combine(nativePrivateDir, module.Name + "_helpers.hpp").CleanPath();
        CppBool32Header = Path.Combine(nativePublicDir, CppBool32HeaderName).CleanPath();

        CHeader = Path.Combine(nativePublicDir, module.Name + ".h").CleanPath();
        CSource = Path.Combine(nativePrivateDir, module.Name + "_capi.cpp").CleanPath();
        CppHeader = Path.Combine(nativePublicDir, module.Name + ".hpp").CleanPath();
        CppSource = Path.Combine(nativePrivateDir, module.Name + ".cpp").CleanPath();
        CppImplHeader = Path.Combine(nativePrivateDir, module.Name + "_impl.hpp").CleanPath();
        CppImplSource = Path.Combine(nativePrivateDir, module.Name + "_impl.cpp").CleanPath();

        // C# check
        if (settings.GenerateCSharpBindings)
        {
            CSharpFile = module.GetStringVariable(VarNames.CSharpOutDir, string.Empty);

            if (string.IsNullOrEmpty(CSharpFile))
                CSharpFile = Path.Combine(OutputDir, "csharp", module.Name + ".cs");

            CSharpFile = CSharpFile.CleanPath();
        }

        // Python check
        if (settings.GeneratePythonBindings)
        {
            PythonCppFile = module.GetStringVariable(VarNames.PythonCppFile, string.Empty);

            if (string.IsNullOrEmpty(PythonCppFile))
                PythonCppFile = Path.Combine(nativePrivateDir, module.Name + "_pybind.cpp");

            PythonCppFile = PythonCppFile.CleanPath();
        }

        // Java check
        if (settings.GenerateJavaBindings)
        {
            JavaJNICppFile = Path.Combine(nativePrivateDir, module.Name + "_jni.cpp").CleanPath();
            JavaOutputDirectory = module.GetStringVariable(VarNames.JavaOutDir, string.Empty);

            if (string.IsNullOrEmpty(JavaOutputDirectory))
            {
                var subPaths = module.JavaPackageName.Split('.');

                string outputPath = Path.Combine(OutputDir, "java");
                foreach (var subPath in subPaths)
                    outputPath = Path.Combine(outputPath, subPath);

                JavaOutputDirectory = outputPath;
            }

            JavaOutputDirectory = JavaOutputDirectory.CleanPath();
        }
    }

    public string OutputDir { get; }

    public string CHeader { get; }
    public string CSource { get; }

    public string CppHeader { get; }
    public string CppSource { get; }
    public string CppImplHeader { get; }
    public string CppImplSource { get; }
    public string CppHelpersHeader { get; }
    public string CppBool32Header { get; }

    public string CSharpFile { get; } = string.Empty;
    public string PythonCppFile { get; } = string.Empty;
    public string JavaJNICppFile { get; set; } = string.Empty;
    public string JavaOutputDirectory { get; set; } = string.Empty;
}