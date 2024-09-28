using brigen.decl;

namespace brigen.gen;

/// <summary>
/// Represents the base class of C/C++ code generators.
/// </summary>
public abstract class NativeCodeGenerator(Module module) : CodeGenerator(module)
{
    protected void GenerateHeaderPreamble(Writer w, bool isCpp)
    {
        w.WriteLine("#if defined(WIN32) || defined(_WIN64)");
        w.WriteLine($"#if {Module.Name}_EXPORTS");
        w.WriteLine($"#define {Module.DllExportApi} __declspec(dllexport)");
        w.WriteLine("#else");
        w.WriteLine($"#define {Module.DllExportApi} __declspec(dllimport)");
        w.WriteLine("#endif");
        w.WriteLine("#else");
        w.WriteLine($"#define {Module.DllExportApi}");
        w.WriteLine("#endif");
        w.WriteLine();

        w.WriteLine("#if defined(_MSC_VER)");
        w.WriteLine($"#define {Module.CallConvApi} __{Module.CppCallConvValue}");
        w.WriteLine("#else");
        w.WriteLine($"#define {Module.CallConvApi}");
        w.WriteLine("#endif");
        w.WriteLine();

        const bool emitLintDisableWarnings = true;

        SortedSet<string> filesToInclude;
        SortedSet<string>? filesToDisableWarningsFor = null;

        if (isCpp)
        {
            filesToInclude = ["utility", "cstddef", "cstdint", Paths.CppBool32HeaderName];

            // In order to generate our own hash implementations, we have to include
            // <functional>, as that is where hash is stored.
            if (Module.CppGenStdHash)
                filesToInclude.Add("functional");
        }
        else
        {
            filesToInclude = ["stddef.h", "stdint.h"];

            if (emitLintDisableWarnings)
                filesToDisableWarningsFor = new SortedSet<string>(filesToInclude);
        }

        foreach (string fileToInclude in filesToInclude)
        {
            w.Write($"#include <{fileToInclude}>");

            if (filesToDisableWarningsFor != null && filesToDisableWarningsFor.Contains(fileToInclude))
            {
                w.Write(" // NOLINT(modernize-deprecated-headers)");
            }

            w.WriteLine();
        }

        w.WriteLine();
    }

    protected void GenerateComment(Writer w, CommentDecl? comment, Decl parentDecl)
    {
        if (comment is null)
            return;

        //w.WriteLine("///");

        PropertyDecl? prop = null;
        if (parentDecl is FunctionDecl function)
            prop = function.OriginalProperty;

        foreach (string line in comment.ContentLines)
        {
            w.Write("/// ");

            if (prop != null && line == comment.ContentLines[0])
                w.Write(prop switch
                {
                    { HasGetter: true, HasSetter: true } => "Gets or sets ",
                    { HasGetter: true } => "Gets ",
                    { HasSetter: true } => "Sets ",
                    _ => throw CompileError.Internal("Property comment")
                });

            w.WriteLine(line);
        }

        foreach ((string name, string desc) in comment.ParameterDescriptions) w.WriteLine($"/// @param {name} {desc}");

        //w.WriteLine("///");
    }

    protected void FinishFile(Writer w, string outputFilename, string? existingContent)
    {
        w.SaveContentsToDisk(outputFilename);

        if (Module.EnableClangFormat)
        {
            string result = ClangFormatFormatter.Format(outputFilename);
            if (!string.IsNullOrEmpty(result))
                File.WriteAllText(outputFilename, result);
        }
    }
}