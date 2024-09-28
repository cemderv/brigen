using brigen.decl;
using brigen.diff;
using brigen.types;
using System.Diagnostics;
using System.Text;

namespace brigen.gen;

public sealed class CppCodeGenerator : NativeCodeGenerator
{
    private static readonly Dictionary<IDataType, string> _primTypeNameTable = new()
    {
        { PrimitiveType.Byte, "uint8_t" },
        { PrimitiveType.Int, "int32_t" },
        { PrimitiveType.Short, "int16_t" },
        { PrimitiveType.Long, "int64_t" },
        { PrimitiveType.Bool, Strings.CppBool32TypeName },
        { PrimitiveType.Float, "float" },
        { PrimitiveType.Double, "double" },
        { PrimitiveType.String, "const char*" },
        { PrimitiveType.Handle, "void*" },
        { PrimitiveType.Void, "void" },
    };

    public enum NameContext
    {
        Neutral = 1,
        StructField,
        FunctionParam,
        FunctionReturnType,
        DelegateReturnType
    }

    [Flags]
    public enum NameContextFlags
    {
        /// <summary>
        /// No special treatment.
        /// </summary>
        None = 0,

        /// <summary>
        /// If context is FunctionReturnType, forces the type to be returned by value.
        /// </summary>
        ForceReturnByValue = 1,

        /// <summary>
        /// If true, prefixes the module namespace to the type name.
        /// </summary>
        ForceModulePrefix = 2,

        /// <summary>
        /// If <see cref="NameContext"/> is <i>FunctionParam</i>, forces the type to be passed by value.
        /// </summary>
        ForcePassByValue = 4
    }

    private const string _todoImplementComment = "// TODO: implement.";

    private readonly DiffDetector _diffDetector;
    private readonly string _getHashCodeFuncName;
    private readonly TempVarNameGen _nameGen;

    public CppCodeGenerator(Module module)
      : base(module)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CppHeader)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CppSource)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CppImplHeader)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CppImplSource)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CppHelpersHeader)!);

        _diffDetector = new DiffDetector(Module.Paths.CppImplHeader, Module.Paths.CppImplSource, Module);

        _getHashCodeFuncName = Module.CppCaseStyle switch
        {
            CaseStyle.PascalCase => "GetHashCode",
            CaseStyle.CamelCase => "getHashCode",
            _ => throw new Exception("Invalid case style")
        };

        _nameGen = new TempVarNameGen(Strings.ForbiddenIdentifierPrefix);
    }

    public override void Generate()
    {
        Logger.LogCategoryGenerationStatus("C++");

        GenerateHelpersHeader();
        GenerateBool32HeaderFile();
        GenerateHppHeaderFile();
        GenerateCppSourceFile();
        GenerateImplHppHeaderFile();
        GenerateImplCppSourceFile();
    }

    public static string GetFunctionSignature(FunctionDecl function, bool inHeader, bool isImpl,
      bool withOutArrayParams = true)
    {
        ClassDecl clss = function.ParentAsClass
            ?? throw new NullReferenceException($"Function {function.Name}: Parent is not an class.");

        string className = isImpl ? clss.ImplClassName : clss.Name;

        var ret = new StringBuilder();

        if (function.IsCtor)
        {
            if (isImpl)
            {
                if (inHeader)
                    ret.Append("static ");

                // Return-type
                ret.Append(className).Append("* ");

                if (!inHeader)
                    ret.Append(className).Append("::");

                ret
                  .Append(Strings.CppImplCtorName)
                  .Append('(')
                  .Append(GetParametersString(function, withOutArrayParams, inHeader))
                  .Append(')');
            }
            else
            {
                if (inHeader)
                    ret.Append("static ");

                // Return-type
                ret
                  .Append(className)
                  .Append(' ');

                if (!inHeader)
                    ret.Append(className).Append("::");

                ret.Append(function.Name)
                  .Append('(')
                  .Append(GetParametersString(function, withOutArrayParams, inHeader))
                  .Append(')');
            }
        }
        else
        {
            var prefix = new StringBuilder();

            if (inHeader && function.IsStatic)
                prefix.Append("static ");

            if (isImpl && inHeader && clss.IsAbstractImpl)
                prefix.Append("virtual ");

            // Return-type
            {
                if (!function.ReturnType.IsArray)
                {
                    prefix.Append(GetTypeName(function.Module!, function.ReturnType, NameContext.FunctionReturnType,
                      NameContextFlags.ForceReturnByValue));
                }

                if (function.HasOutReturnParam && function.ReturnType.IsArray)
                {
                    prefix.Clear();
                    prefix.Append("void");
                }

                prefix.Append(' ');
            }

            if (!inHeader)
                prefix.Append(className).Append("::");

            ret.Append(prefix)
              .Append(function.Name)
              .Append('(')
              .Append(GetParametersString(function, withOutArrayParams, inHeader))
              .Append(')');

            if (function.IsConst)
                ret.Append(" const");

            if (isImpl && inHeader && clss.IsAbstractImpl)
                ret.Append(" = 0");
        }

        return ret.ToString();
    }

    public static string GetParametersString(FunctionDecl function, bool withOutArrayParams, bool inHeader)
    {
        var ret = new StringBuilder();

        foreach (FunctionParamDecl param in function.Parameters)
        {
            string typeName = GetTypeName(function.Module!, param.Type, NameContext.FunctionParam,
              NameContextFlags.ForceReturnByValue);

            ret.Append($"{typeName} {param.Name}");

            if (param.Type.IsArray)
                ret.Append($", uint32_t {param.Name}Size");

            if (param != function.Parameters[^1])
                ret.Append(", ");
        }

        if (withOutArrayParams && function.HasOutReturnParam && function.ReturnType is ArrayType outArrayType)
        {
            string typeName = GetTypeName(function.Module!, outArrayType.ElementType, NameContext.FunctionParam,
              NameContextFlags.ForceReturnByValue | NameContextFlags.ForcePassByValue);

            if (function.Parameters.Any())
                ret.Append(", ");

            ret.Append($"{typeName}* resultArray, uint32_t* resultArraySize");
        }

        // Write all system value parameters
        {
            var delegateParams = function.DelegateParameters.ToArray();

            if (delegateParams.Length != 0)
                ret.Append(", ");

            foreach (FunctionParamDecl param in delegateParams)
            {
                ret.Append("void* ")
                  .Append(param.Name)
                  .Append("_sysValue");

                if (inHeader)
                    ret.Append(" = nullptr");

                if (param != delegateParams[^1])
                    ret.Append(", ");
            }
        }

        return ret.ToString();
    }

    public static void GenerateDelegateParameters(Writer w, DelegateDecl delg, string sysValueVarName,
      bool generateAnonParamNames, List<string>? dstAnonParamNames, TempVarNameGen? nameGenerator,
      bool forceModulePrefix)
    {
        Module module = delg.Module!;

        // sys value
        w.Write($"void* {sysValueVarName}");
        if (delg.Parameters.Any())
            w.Write(", ");

        string modulePrefix = forceModulePrefix ? $"{module.Name}::" : string.Empty;

        foreach (FunctionParamDecl param in delg.Parameters)
        {
            if (param.Type == PrimitiveType.String && param.Type.IsArray)
            {
                Debugger.Break();
                if (param.Type.IsArray)
                    w.Write($"{modulePrefix}ArrayView<const char*>");
            }
            else
            {
                var flags = NameContextFlags.ForceReturnByValue;
                if (forceModulePrefix)
                    flags |= NameContextFlags.ForceModulePrefix;

                if (param.Type.IsArray)
                    flags |= NameContextFlags.ForcePassByValue;

                string typeName = GetTypeName(module, param.Type, NameContext.FunctionParam, flags);

                w.Write(param.Type.IsArray ? $"{modulePrefix}ArrayView<{typeName}>" : typeName);
            }

            string paramName = generateAnonParamNames && nameGenerator != null ? nameGenerator.CreateNext() : param.Name;

            dstAnonParamNames?.Add(paramName);

            w.Write(" ").Write(paramName);

            if (param != delg.Parameters[^1])
                w.Write(", ");
        }
    }


    /// <summary>
    /// Gets the equivalent name of a specific type in C++.
    /// </summary>
    /// <param name="module">The module from which the type is referenced.</param>
    /// <param name="type">The qualified type.</param>
    /// <param name="context">The context for which to get the name.</param>
    /// <param name="flags">Flags</param>
    /// <returns></returns>
    public static string GetTypeName(Module module, IDataType type, NameContext context,
      NameContextFlags flags = NameContextFlags.None)
    {
        if (type is ArrayType arrayType)
        {
            var elemType = arrayType.ElementType;
            Debug.Assert(!elemType.IsArray);
            string str = GetTypeName(module, elemType, context, flags);

            if (context is NameContext.FunctionParam)
            {
                if (elemType == PrimitiveType.String)
                {
                    return $"const char* const*";
                }
                else if (elemType.IsUserDefined)
                {
                    str = str.TrimEnd('&');
                    return $"{str}*";
                }
                else if (elemType == PrimitiveType.Handle)
                {
                    return $"const void* const*";
                }
                else
                {
                    return $"const {str}*";
                }
            }
            else
                throw new NotImplementedException("unknown type");
        }

        const int fNone = 0;
        const int fConst = 1;
        const int fPointer = 2;
        const int fReference = 4;

        int localFlags = fNone;

        if (type.IsStruct && context != NameContext.StructField)
        {
            localFlags |= fConst;
            localFlags |= fReference;
        }

        if (type.IsClass)
        {
            if (context == NameContext.FunctionParam)
            {
                localFlags |= fConst;
                localFlags &= ~fPointer;
                localFlags |= fReference;
            }
            else if (context != NameContext.StructField)
            {
                localFlags &= ~fConst;
                localFlags |= fPointer;
            }
        }

        bool isReturnTypeContext =
          context is NameContext.FunctionReturnType or NameContext.DelegateReturnType;

        if (isReturnTypeContext && flags.HasFlag(NameContextFlags.ForceReturnByValue))
            localFlags = fNone;

        if (context == NameContext.FunctionParam && flags.HasFlag(NameContextFlags.ForcePassByValue))
            localFlags = fNone;

        // If neutral type name was requested, remove all modifiers.
        if (context == NameContext.Neutral)
            localFlags = fNone;

        var ret = new StringBuilder();

        if (type is TypeDecl typeDecl)
        {
            Debug.Assert(typeDecl.Module != null);
            bool needModulePrefix = flags.HasFlag(NameContextFlags.ForceModulePrefix) || module != typeDecl.Module;

            if (needModulePrefix)
            {
                ret.Append(typeDecl.Module!.Name);
                ret.Append("::");
            }

            ret.Append(typeDecl.Name);
        }
        else
        {
            if (_primTypeNameTable.TryGetValue(type, out string? primTypeName))
                ret.Append(primTypeName);
            else
                ret.Append(type.Name);
        }

        if ((localFlags & fConst) == fConst)
            ret.Insert(0, "const ");

        if ((localFlags & fPointer) == fPointer)
            ret.Append('*');

        if ((localFlags & fReference) == fReference)
            ret.Append('&');

        return ret.ToString();
    }

    private void GenerateForwardDeclarations(Writer w)
    {
        var decls = new List<TypeDecl>(8);
        decls.AddRange(from decl in Module.Decls
                       where decl is StructDecl or ClassDecl
                       select decl as TypeDecl);

        decls.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));

        foreach (TypeDecl decl in decls)
        {
            w.WriteLine($"class {decl.Name};");

            if (decl is ClassDecl clss)
                w.WriteLine($"class {clss.ImplClassName};");
        }

        w.WriteLine();
    }

    private void GenerateHppHeaderFile()
    {
        Logger.LogFileGenerationStatus("Header", Module.Paths.CppHeader);

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CppHeader,
          [$"This is the header file for the C++ API of {Module.Name}."],
          "//", false);

        BeginCHeader(w);

        if (GenerateExtraIncludesInCppHeaderFile(w))
            w.WriteLine();

        {
            var replacements = new Dictionary<string, string>
      {
        { "${IS_VALID}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.IsValid, Module) },
        { "${GET_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.GetImpl, Module) },
        { "${SET_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.SetImpl, Module) },
        { "${DROP_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.DropImpl, Module) }
      };

            GenerateTemplateString(w, TemplateStrings.ClassPrereqs, replacements);
            w.WriteLine();
        }

        if (Module.AllEnums.Any(e => e.IsFlags))
        {
            GenerateTemplateString(w, TemplateStrings.CppDefineEnumOps, null);
            w.WriteLine();
        }

        BeginNamespace(w);

        GenerateForwardDeclarations(w);

        foreach (Decl decl in Module.Decls)
        {
            w.ClearWriteMarker();

            if (decl is EnumDecl enm)
                GenerateEnumInHeader(w, enm);
            else if (decl is StructDecl strct)
                GenerateStructInHeader(w, strct);
            else if (decl is ClassDecl clss)
                GenerateClassInHeader(w, clss);
            else if (decl is DelegateDecl delg)
                GenerateDelegateInHeader(w, delg);

            if (w.HasWrittenAnything)
                w.WriteLine();

            w.ClearWriteMarker();
        }

        GenerateFreeFunctionDeclarations(w);
        w.WriteLine();

        GenerateFreeFunctionDefinitions(w);

        EndNamespace(w);
        w.WriteLine();

        // See if we have to generate hash implementations.
        if (Module.CppGenStdHash)
            GenerateStdHashImplementationsForAllStructs(w);

        GenerateTemplateString(w, TemplateStrings.CppUndefs, null);
        EndCHeader(w);
        FinishFile(w, Module.Paths.CppHeader, string.Empty);
    }

    private void GenerateBool32HeaderFile()
    {
        var w = new Writer();

        w.WriteLine("#pragma once");
        w.WriteLine();

        w.WriteLine("#include <cstddef>");
        w.WriteLine("#include <cstdint>");
        w.WriteLine();

        GenerateTemplateString(w, TemplateStrings.CppBool32, null);
        w.WriteLine();

        w.SaveContentsToDisk(Module.Paths.CppBool32Header);
    }

    private void GenerateCppSourceFile()
    {
        Logger.LogFileGenerationStatus("Source", Module.Paths.CppSource);

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CppSource,
          [$"This is the source file for the C++ API of {Module.Name}."], "//", false);

        w.WriteLine($"#include \"{Module.Name}.hpp\"");
        w.WriteLine($"#include \"{Module.Name}_impl.hpp\"");
        w.WriteLine();

        BeginNamespace(w);

        foreach (ClassDecl clss in Module.AllClasses.Where(i => !i.IsStatic))
            w.WriteLine($"DEFINE_OBJ_CONSTRUCT({clss.Name})");

        w.WriteLine();

        foreach (ClassDecl clss in Module.AllClasses)
        {
            w.ClearWriteMarker();

            // Constructors
            foreach (FunctionDecl ctor in clss.Ctors)
            {
                GenerateFunctionSignature(w, ctor, false, false);
                GenerateClassConstructorBody(w, ctor);

                if (ctor != clss.Ctors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // Functions
            foreach (FunctionDecl function in clss.AllExportedFunctionsWithoutCtors)
            {
                GenerateFunctionSignature(w, function, false, false);
                GenerateFunctionBody(w, function);

                if (function != clss.AllExportedFunctionsWithoutCtors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // GetHashCode()
            if (!clss.IsStatic)
            {
                w.Write($"size_t {clss.CppName}::{_getHashCodeFuncName}() const ");
                w.OpenBrace();
                w.WriteLine("return static_cast<size_t>(*reinterpret_cast<const uintptr_t*>(m_Impl));");
                w.CloseBrace();
            }

            if (clss != Module.AllClasses[^1])
                w.WriteLine();
        }

        EndNamespace(w);

        FinishFile(w, Module.Paths.CppSource, string.Empty);
    }


    private void GenerateHelpersHeader()
    {
        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CppSource,
          [$"  helpers for {Module.Name}."], "//", false);

        w.WriteLine("#pragma once");
        w.WriteLine();

        w.WriteLine("// clang-format off");
        w.WriteLine();

        foreach (string dep in new[] { "atomic", "cassert" })
            w.WriteLine($"#include <{dep}>");

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        BeginNamespace(w);

        {
            var replacements = new Dictionary<string, string>
      {
        { "${IS_VALID}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.IsValid, Module) },
        { "${GET_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.GetImpl, Module) },
        { "${SET_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.SetImpl, Module) },
        { "${DROP_IMPL}", NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.DropImpl, Module) }
      };

            GenerateTemplateString(w, TemplateStrings.ObjectImplBase, replacements);
            w.WriteNewlineIfWrittenAnythingAndResetMarker();
        }

        GenerateTemplateString(w, TemplateStrings.GetArrayHelper, null);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        GenerateTemplateString(w, TemplateStrings.CppInvokeFunction, null);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        GenerateTemplateString(w, TemplateStrings.CppScopeGuard, null);

        EndNamespace(w);

        FinishFile(w, Module.Paths.CppHelpersHeader, null);
    }


    private void GenerateImplHppHeaderFile()
    {
        Logger.LogFileGenerationStatus("Impl Header", Module.Paths.CppImplHeader);

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CppImplHeader,
          [
        $"This is the header file for the C++ implementation of {Module.Name}.",
        "You may add additional members to classes that are defined in this file."
          ], "//", true);

        w.WriteLine("#pragma once");

        if (!Module.EnableClangFormat)
        {
            w.WriteLine();
            w.WriteLine("// clang-format off");
        }

        w.WriteLine();
        w.WriteLine($"#include \"{Module.Name}.hpp\"");
        w.WriteLine(
          $"#include \"{Path.GetRelativePath(Path.GetDirectoryName(Module.Paths.CppImplHeader)!, Module.Paths.CppHelpersHeader)}\"");
        w.WriteLine();

        GenerateUserDefinedPreamble(w, DiffDetectorTarget.ImplHeader);

        BeginNamespace(w);

        // Impl classes
        foreach (ClassDecl clss in Module.AllClasses)
        {
            w.Write($"class {clss.ImplClassName}");

            if (clss.IsStatic)
                w.Write(" final");
            else
                w.Write(" : public ObjectImplBase");

            w.WriteLine();
            w.WriteLine("{");

            // structs
            {
                w.Indent();
                _diffDetector.GenerateImplClassExtraStructs(w, clss);
                w.Unindent();
            }

            w.WriteLine("public:");
            w.Indent();

            // Constructors
            foreach (FunctionDecl ctor in clss.Ctors)
            {
                GenerateFunctionSignature(w, ctor, true, true);
                w.WriteLine(";");
                w.WriteLine();
            }

            w.ClearWriteMarker();

            // Destructor
            if (!clss.IsStatic)
            {
                FunctionInfo? dtorInfo = _diffDetector.GetDtorInfo(DiffDetectorTarget.ImplHeader, clss);

                if (dtorInfo == null)
                    w.WriteLine($"~{clss.ImplClassName}() noexcept override;");
                else
                    w.WriteLine($"{dtorInfo.Content};");
            }

            if (w.HasWrittenAnything)
                w.WriteLine();

            w.ClearWriteMarker();

            // Functions
            foreach (FunctionDecl func in clss.AllExportedFunctions)
            {
                if (func.IsCtor)
                    continue;

                GenerateFunctionSignature(w, func, true, true);
                w.WriteLine(";");

                if (func != clss.AllExportedFunctions[^1])
                    w.WriteLine();
            }

            // Extra enums
            _diffDetector.GenerateImplClassExtraEnums(w, clss);

            // Extra functions
            GenerateExtraFunctionsForClass(w, DiffDetectorTarget.ImplHeader, clss);

            // Extra fields
            _diffDetector.GenerateImplClassExtraFields(w, clss);

            w.Unindent();
            w.WriteLine("};");

            if (clss != Module.AllClasses[^1])
                w.WriteLine();
        }

        EndNamespace(w);
        w.WriteLine();

        // See if we have to generate hash implementations.
        // if (Module.CppGenStdHash)
        //   GenerateStdHashImplementationsForAllStructs(w);

        FinishFile(w, Module.Paths.CppImplHeader, _diffDetector.GetImplHeaderFileContents());
    }


    private void GenerateImplCppSourceFile()
    {
        Logger.LogFileGenerationStatus("Impl Source", Module.Paths.CppImplSource);

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CppImplSource,
          [
        $"This is the source file for C++ implementation of {Module.Name}.",
        "You may implement the function stubs that were generated."
          ], "//", true);

        if (!Module.EnableClangFormat)
        {
            w.WriteLine();
            w.WriteLine("// clang-format off");
        }

        w.WriteLine();
        w.WriteLine($"#include \"{Module.Name}_impl.hpp\"");
        w.WriteLine();

        GenerateUserDefinedPreamble(w, DiffDetectorTarget.ImplSource);

        BeginNamespace(w);

        foreach (ClassDecl clss in Module.AllClasses)
        {
            w.ClearWriteMarker();

            // ------------------------
            // Constructors
            // ------------------------

            foreach (FunctionDecl ctor in clss.Ctors)
            {
                FunctionInfo? ctorInfo = _diffDetector.GetFunctionInfo(DiffDetectorTarget.ImplSource, ctor);

                if (ctorInfo == null)
                    GenerateImplSourceClassConstructorBody(w, ctor);
                else
                    w.WriteLine(ctorInfo.Content);

                if (ctor != clss.Ctors[^1])
                    w.WriteLine();
            }

            if (w.HasWrittenAnything)
                w.WriteLine();

            w.ClearWriteMarker();

            // ------------------------
            // Destructor
            // ------------------------

            if (!clss.IsStatic)
            {
                FunctionInfo? dtorInfo = _diffDetector.GetDtorInfo(DiffDetectorTarget.ImplSource, clss);

                if (dtorInfo == null)
                    w.WriteLine($"{clss.ImplClassName}::~{clss.ImplClassName}() noexcept = default;");
                else
                    w.WriteLine(dtorInfo.Content);
            }

            if (w.HasWrittenAnything)
                w.WriteLine();

            w.ClearWriteMarker();

            // ------------------------
            // Functions
            // ------------------------
            if (!clss.IsAbstractImpl)
            {
                var funcs = clss.AllExportedFunctions.Where(f => !f.IsCtor).ToList();

                foreach (FunctionDecl func in funcs)
                {
                    FunctionInfo? funcInfo = _diffDetector.GetFunctionInfo(DiffDetectorTarget.ImplSource, func);

                    if (funcInfo == null)
                    {
                        string sig = GetFunctionSignature(func, false, true);
                        w.WriteLine(sig);
                        string returnTypeName = sig[..sig.IndexOf(' ')].Trim();

                        w.OpenBrace();
                        w.WriteLine(_todoImplementComment);

                        if (returnTypeName != "void")
                        {
                            if (returnTypeName[^1] == '*')
                            {
                                // A pointer. Return null.
                                w.WriteLine("return nullptr;");
                            }
                            else if (returnTypeName[^1] != '&')
                            {
                                // We're not returning a pointer, and also not a reference.
                                // Since we cannot return a default reference value (as it has to
                                // be named), we will not return anything for references values.
                                // Let the developer deal with the compiler's errors.
                                w.WriteLine("return {};");
                            }
                        }

                        w.CloseBrace();
                    }
                    else
                    {
                        w.WriteLine(funcInfo.Content);
                    }

                    if (func != funcs[^1])
                    {
                        w.WriteLine();
                        w.WriteLine();
                    }
                }
            }

            if (w.HasWrittenAnything)
            {
                w.WriteLine();
                w.WriteLine();
            }

            w.ClearWriteMarker();

            // Additional functions that exist in the C++ file, but are not defined
            // in the class (preserve user-defined functions).
            {
                List<FunctionInfo> funcs = _diffDetector.GetExtraFunctionsForClass(DiffDetectorTarget.ImplSource, clss);

                if (funcs.Count > 0)
                    w.WriteLine();

                foreach (FunctionInfo funcInfo in funcs)
                {
                    w.WriteLine(funcInfo.Content);
                    if (funcInfo != funcs[^1])
                    {
                        w.WriteLine();
                        w.WriteLine();
                    }
                }
            }

            if (w.HasWrittenAnything)
            {
                w.WriteLine();
                w.WriteLine();
            }

            w.ClearWriteMarker();
        }

        EndNamespace(w);

        FinishFile(w, Module.Paths.CppImplSource, _diffDetector.GetImplSourceFileContents());
    }


    private void GenerateImplSourceClassConstructorBody(Writer w, FunctionDecl ctor)
    {
        ClassDecl clss = ctor.ParentAsClass ??
            throw new NullReferenceException($"Function {ctor.Name}: Parent class was null.");

        GenerateFunctionSignature(w, ctor, false, true);
        w.WriteLine();
        w.OpenBrace();

        if (clss.IsAbstractImpl)
        {
            w.WriteLine(_todoImplementComment);
            w.WriteLine("return nullptr;");
        }
        else
        {
            w.WriteLine($"auto impl = new (std::nothrow) {clss.ImplClassName}();");
            w.WriteLine();
            w.Write("if (impl == nullptr) ");
            w.OpenBrace();
            w.WriteLine(_todoImplementComment);
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine("return impl;");
        }

        w.CloseBrace();
    }


    private void GenerateEnumInHeader(Writer w, EnumDecl enm)
    {
        GenerateComment(w, enm.Comment, enm);

        w.Write($"enum class {enm.Name} : int32_t ");
        w.OpenBrace();

        foreach (EnumMemberDecl member in enm.Members)
        {
            GenerateComment(w, member.Comment, member);
            w.WriteLine($"{member.Name} = {member.Value!.Value},");
        }

        w.CloseBrace(true);

        if (enm.IsFlags)
        {
            w.WriteLine();
            w.WriteLine($"{Module.UpperName}_DEFINE_ENUM_FLAG_OPS({enm.Name})");
        }
    }


    private void GenerateStructInHeader(Writer w, StructDecl strct)
    {
        GenerateComment(w, strct.Comment, strct);

        w.WriteLine($"class {strct.Name}");
        w.WriteLine('{');
        w.WriteLine("public:");
        w.Indent();

        w.ClearWriteMarker();

        foreach (StructFieldDecl field in strct.Fields)
        {
            GenerateComment(w, field.Comment, field);

            string typeName = GetTypeName(strct.Module!, field.Type, NameContext.StructField,
              NameContextFlags.ForceReturnByValue);
            w.WriteLine($"{typeName} {field.NameInCpp};");
        }

        w.CloseBrace(true);
    }

    private void GenerateClassInHeader(Writer w, ClassDecl clss)
    {
        GenerateComment(w, clss.Comment, clss);

        w.Write($"class {Module.DllExportApi} {clss.Name}");

        if (clss.IsStatic)
            w.Write(" final");

        w.WriteLine();
        w.WriteLine("{\npublic:");
        w.Indent();

        if (clss.IsStatic)
        {
            w.WriteLine($"{clss.Name}() = delete;");
        }
        else
        {
            w.WriteLine($"{Module.UpperName}_CLASS_({clss.Name})");
            w.WriteLine();
        }

        w.ClearWriteMarker();

        // Constructors
        foreach (FunctionDecl ctor in clss.Ctors)
        {
            GenerateComment(w, ctor.Comment, ctor);

            GenerateFunctionSignature(w, ctor, true, false);
            w.WriteLine(";");

            if (ctor != clss.Ctors[^1])
                w.WriteLine();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Generate all functions (including properties) together,
        // because we don't have properties in C++. They're normal functions (getters
        // and setters).
        foreach (FunctionDecl function in clss.AllExportedFunctions)
        {
            if (function.IsCtor)
                continue;

            GenerateComment(w, function.Comment, function);

            GenerateFunctionSignature(w, function, true, false);
            w.WriteLine(';');

            // If this function returns an array, generate a vector-based overload, if desired by the module.
            if (function.HasOutReturnParam && function.ReturnType.IsArray)
            {
                w.WriteNewlineIfWrittenAnythingAndResetMarker();

                if (Module.CppVectorSupport)
                    GenerateStdVectorBasedOverloadedFunction(w, function);

                w.WriteNewlineIfWrittenAnythingAndResetMarker();
            }

            if (function != clss.AllExportedFunctions[^1])
                w.WriteLine();
        }

        // GetHashCode()
        if (!clss.IsStatic)
        {
            w.WriteNewlineIfWrittenAnythingAndResetMarker();
            w.WriteLine($"size_t {_getHashCodeFuncName}() const;");
        }

        w.CloseBrace(true);
    }


    private void GenerateDelegateInHeader(Writer w, DelegateDecl delg)
    {
        string returnTypeName = GetTypeName(delg.Module!, delg.ReturnType!, NameContext.DelegateReturnType,
          NameContextFlags.ForceReturnByValue);

        w.Write($"typedef {returnTypeName}({Module.CallConvApi}*{delg.Name})(");
        GenerateDelegateParameters(w, delg, "sysValue", false, null, null, false);
        w.WriteLine(");");
    }


    private void GenerateClassConstructorBody(Writer w, FunctionDecl function)
    {
        _nameGen.Clear();
        w.OpenBrace();

        // pImpl Construction
        {
            ClassDecl clss = function.ParentAsClass!;

            // Constructor call
            w.Write($"return {clss.Name}({clss.ImplClassName}::{Strings.CppImplCtorName}(");
            GenerateFunctionCallArguments(w, function,
              GenerateArgumentsFlags.WithOutArrayParams | GenerateArgumentsFlags.WithDelgSysValues);
            w.WriteLine("));");
        }

        w.CloseBrace();
    }


    private static void GenerateFunctionCallArguments(Writer w, FunctionDecl function, GenerateArgumentsFlags flags)
    {
        foreach (FunctionParamDecl param in function.Parameters)
        {
            if (param.Type.IsDelegate && (flags & GenerateArgumentsFlags.PassFuncPtrsAsNull) ==
                GenerateArgumentsFlags.PassFuncPtrsAsNull)
                w.Write("nullptr");
            else
                w.Write(param.Name);

            if (param.Type.IsArray)
                w.Write($", {param.Name}Size");

            if (param != function.Parameters[^1])
                w.Write(", ");
        }

        if (flags.HasFlag(GenerateArgumentsFlags.WithOutArrayParams) && function.HasOutReturnParam &&
            function.ReturnType.IsArray)
        {
            if (function.Parameters.Any())
                w.Write(", ");

            w.Write("resultArray, resultArraySize");
        }

        if (flags.HasFlag(GenerateArgumentsFlags.WithDelgSysValues))
        {
            FunctionParamDecl[] delegateParams = function.DelegateParameters.ToArray();

            if (delegateParams.Length != 0)
                w.Write(", ");

            foreach (FunctionParamDecl param in delegateParams)
            {
                w.Write($"{param.Name}_sysValue");
                if (param != delegateParams[^1])
                    w.Write(", ");
            }
        }
    }


    private void GenerateFunctionSignature(Writer w, FunctionDecl function, bool inHeader, bool isImpl) =>
      w.Write(GetFunctionSignature(function, inHeader, isImpl));


    private void GenerateFunctionBody(Writer w, FunctionDecl function)
    {
        _nameGen.Clear();
        w.OpenBrace();
        string implClassName = function.ParentAsClass!.ImplClassName;
        string dPtrVarname = string.Empty;

        if (!function.IsStatic)
        {
            dPtrVarname = _nameGen.CreateNext();
            w.WriteLine($"const auto {dPtrVarname} = static_cast<{implClassName}*>(m_Impl);");
        }

        bool hasReturnStatement = function.HasOutReturnParam
          ? !function.ReturnType.IsArray
          : function.HasNonVoidReturnType;

        if (hasReturnStatement)
            w.Write("return ");

        if (function.IsStatic)
            w.Write(implClassName).Write("::");
        else
            w.Write(dPtrVarname).Write("->");

        w.Write(function.Name + '(');
        GenerateFunctionCallArguments(w, function,
          GenerateArgumentsFlags.WithOutArrayParams | GenerateArgumentsFlags.WithDelgSysValues);
        w.WriteLine(");");

        w.CloseBrace();
    }


    private void GenerateParameters(Writer w, FunctionDecl function, bool withOutArrayParams, bool inHeader) =>
      w.Write(GetParametersString(function, withOutArrayParams, inHeader));


    private void BeginCHeader(Writer w)
    {
        string includeGuardName = Module.UpperName + "_HPP_INCLUDED";

        w.WriteLine($"#ifndef {includeGuardName}");
        w.WriteLine($"#define {includeGuardName}");
        w.WriteLine();

        GenerateHeaderPreamble(w, true);
    }


    private void EndCHeader(Writer w)
    {
        w.WriteLine();
        w.Write($"#endif // {Module.UpperName}_HPP_INCLUDED\n");
        w.WriteLine();
    }


    private void BeginNamespace(Writer w)
    {
        w.WriteLine($"namespace {Module.Name}");
        w.WriteLine('{');
    }


    private void EndNamespace(Writer w)
      => w.WriteLine($"}} // namespace {Module.Name}");


    private void GenerateUserDefinedPreamble(Writer w, DiffDetectorTarget target)
    {
        string? existingContent = _diffDetector.HasPreamble(target);
        if (existingContent == null)
        {
            w.WriteLine(DiffDetector.preambleStartMarker);
            w.WriteLine("// TODO: insert any #includes and related code here.");
            w.WriteLine(DiffDetector.preambleEndMarker);
        }
        else
        {
            w.WriteLine(existingContent);
        }

        w.WriteLine();
    }


    // TODO: move this function to DiffDetector
    private void GenerateExtraFunctionsForClass(Writer w, DiffDetectorTarget target, ClassDecl clss)
    {
        // Additional functions that exist in the C++ file, but are not defined
        // in the class (preserve user-defined functions).

        List<FunctionInfo> funcs = _diffDetector.GetExtraFunctionsForClass(target, clss);

        if (funcs.Count == 0)
            return;

        w.WriteLine();

        if (funcs.Count > 0)
        {
            w.WriteLine("// -------------");
            w.WriteLine("// Extra methods");
            w.WriteLine("// -------------");
        }

        var currentVis = (Visibility)(-1);

        foreach (FunctionInfo funcInfo in funcs)
        {
            if (funcInfo.Visibility != currentVis)
            {
                w.Unindent();
                w.Write(DiffDetector.GetVisibilityCppString(funcInfo.Visibility));
                w.WriteLine(':');
                w.Indent();
                currentVis = funcInfo.Visibility;
            }

            w.Write(funcInfo.Content);
            w.Write(';');
            w.WriteLine();

            if (funcInfo != funcs[^1])
                w.WriteLine();
        }
    }


    private bool GenerateExtraIncludesInCppHeaderFile(Writer w)
    {
        var stringVec = new List<string>();

        if (Module.CppGenStdHash)
        {
            stringVec.Add("<initializer_list>");
            stringVec.Add("<vector>");
        }

        foreach (string inc in stringVec)
            w.WriteLine($"#include {inc}");

        return stringVec.Count > 0;
    }


    private void GenerateStdHashImplementationsForAllStructs(Writer w)
    {
        if (!Module.AllStructs.Any())
            return;

        w.WriteLine("namespace std");
        w.WriteLine('{');

        foreach (StructDecl strct in Module.AllStructs)
        {
            GenerateStdHashImplementationForStruct(w, strct);

            if (strct != Module.AllStructs[^1])
                w.WriteLine();
        }

        w.WriteLine('}');
    }


    private void GenerateStdHashImplementationForStruct(Writer w, StructDecl strct)
    {
        string paramName = strct.Name.Cased(CaseStyle.CamelCase);
        string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
          NameContextFlags.ForceModulePrefix);

        w.Write($"template <> struct hash<{structNameInCpp}> ");
        w.OpenBrace();
        w.Write($"size_t operator()(const {structNameInCpp}& {paramName}) const ");
        w.OpenBrace();
        w.WriteLine($"return {Module.Name}::{_getHashCodeFuncName}({paramName});");
        w.CloseBrace();

        w.CloseBrace(true);
    }

    private void GenerateStdVectorBasedOverloadedFunction(Writer w, FunctionDecl function)
    {
        const string returnValueName = "returnValue_";
        const string returnValueSizeName = "returnValueSize_";

        var outArrayType = function.ReturnType as ArrayType;
        Debug.Assert(outArrayType != null);

        string typeName = GetTypeName(function.Module!, outArrayType.ElementType, NameContext.FunctionReturnType,
          NameContextFlags.ForceReturnByValue);

        w.Write($"inline std::vector<{typeName}> {function.Name}(");
        GenerateParameters(w, function, false, true);
        w.Write(")");

        if (function.IsConst)
            w.Write(" const");

        w.WriteLine();
        w.OpenBrace();

        w.WriteLine($"std::vector<{typeName}> {returnValueName};");
        w.WriteLine($"uint32_t {returnValueSizeName}{{}};");

        // First pass: get the size of the array.
        {
            w.Write($"this->{function.Name}(");
            GenerateFunctionCallArguments(w, function, GenerateArgumentsFlags.PassFuncPtrsAsNull);

            if (function.Parameters.Any())
                w.Write(", ");

            w.WriteLine($"nullptr, &{returnValueSizeName});");
        }

        // Resize the vector.
        w.WriteLine($"{returnValueName}.resize(static_cast<size_t>({returnValueSizeName}));");

        // Second pass: fill the vector.
        {
            w.Write($"this->{function.Name}(");
            GenerateFunctionCallArguments(w, function, GenerateArgumentsFlags.None);

            if (function.Parameters.Any())
                w.Write(", ");

            w.Write($"{returnValueName}.data(), &{returnValueSizeName}");

            FunctionParamDecl[] delgParams = function.DelegateParameters.ToArray();

            if (delgParams.Length != 0)
                w.Write(", ");

            foreach (FunctionParamDecl delgParam in delgParams)
            {
                w.Write($"{delgParam.Name}_sysValue");
                if (delgParam != delgParams[^1])
                    w.Write(", ");
            }

            w.WriteLine(");");
        }

        // Return the vector.
        w.WriteLine($"return {returnValueName};");

        w.CloseBrace();
    }

    private void GenerateTemplateString(Writer w, string str, Dictionary<string, string>? replacements)
    {
        str = str
          .Trim(' ')
          .Trim('\n')
          .Trim(' ')
          .Trim('\n')
          .Replace("${MOD}", Module.UpperName)
          .Replace("${mod}", Module.Name);

        if (replacements != null)
            foreach ((string key, string value) in replacements)
                str = str.Replace(key, value);

        w.WriteLine(str);
    }


    private void GenerateFreeFunctionDeclarations(Writer w)
    {
        // operator== and operator!= declarations
        {
            foreach (StructDecl strct in Module.AllStructs)
            {
                // operator==
                w.WriteLine($"bool operator==(const {strct.Name}& lhs, const {strct.Name}& rhs);");

                w.WriteLine();

                // operator!=
                w.WriteLine($"bool operator!=(const {strct.Name}& lhs, const {strct.Name}& rhs);");

                if (strct != Module.AllStructs[^1])
                    w.WriteLine();
            }
        }

        w.WriteLine();

        // GetHashCode() declaration
        {
            foreach (StructDecl strct in Module.AllStructs)
            {
                w.WriteLine($"// Computes the hash code for a specific {strct.Name}.");
                w.WriteLine($"// @param obj The {strct.Name} whose hash code should be computed.");
                w.WriteLine($"size_t {_getHashCodeFuncName}(const {strct.Name}& obj);");

                if (strct != Module.AllStructs[^1])
                    w.WriteLine();
            }
        }
    }


    private void GenerateFreeFunctionDefinitions(Writer w)
    {
        w.ClearWriteMarker();

        // operator== and operator!= definitions
        {
            foreach (StructDecl strct in Module.AllStructs)
            {
                // operator==
                {
                    w.Write($"inline bool operator==(const {strct.Name}& lhs, const {strct.Name}& rhs) ");
                    w.OpenBrace();
                    w.Write("return ");

                    if (strct.Fields.Count > 1)
                    {
                        w.WriteLine();
                        w.Indent();
                    }

                    foreach (StructFieldDecl field in strct.Fields)
                    {
                        w.Write($"lhs.{field.NameInCpp} == rhs.{field.NameInCpp}");
                        if (field == strct.Fields[^1])
                            w.Write(';');

                        w.WriteLine();

                        if (field != strct.Fields[^1])
                            w.Write("&& ");
                    }

                    if (strct.Fields.Count > 1)
                        w.Unindent();

                    w.CloseBrace();
                }

                w.WriteLine();

                // operator!=
                {
                    w.Write($"inline bool operator!=(const {strct.Name}& lhs, const {strct.Name}& rhs) ");
                    w.OpenBrace();
                    w.WriteLine("return !(lhs == rhs);");
                    w.CloseBrace();
                }

                if (strct != Module.AllStructs[^1])
                    w.WriteLine();
            }
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // GetHashCode() definition
        {
            foreach (StructDecl strct in Module.AllStructs)
            {
                w.Write($"inline size_t {_getHashCodeFuncName}(const {strct.Name}& obj) ");
                w.OpenBrace();

                w.WriteLine($"size_t hash = {Module.HashFirstPrime}u;");
                foreach (StructFieldDecl field in strct.Fields)
                {
                    w.Write($"hash = hash * {Module.HashSecondPrime}u + ");

                    if (field.Type is TypeDecl td and not EnumDecl)
                    {
                        if (td is ClassDecl)
                            w.Write($"obj.{field.NameInCpp}.{_getHashCodeFuncName}()");
                        else
                            w.Write($"{_getHashCodeFuncName}(obj.{field.NameInCpp})");
                    }
                    else
                    {
                        string typeName = GetTypeName(Module, field.Type, NameContext.StructField);
                        if (field.Type == PrimitiveType.Bool)
                            typeName = "bool";

                        w.Write($"std::hash<{typeName}>()(obj.{field.NameInCpp})");
                    }

                    w.WriteLine(';');
                }

                w.WriteLine("return hash;");
                w.CloseBrace();

                if (strct != Module.AllStructs[^1])
                    w.WriteLine();
            }
        }
    }

    private static string ReferenceType(Module from, TypeDecl typeDecl, bool forceModulePrefix = false)
      => forceModulePrefix || @from != typeDecl.Module ? $"{typeDecl.Module!.Name}::{typeDecl.Name}" : typeDecl.Name;

    public static void BeginScopeGuardBlock(Module module, Writer w, string lambdaCapture = "&") =>
      w.Write($"{module.Name}::{Strings.CppInternalNamespace}::make_scope_guard([{lambdaCapture}] ").OpenBrace();

    public static void EndScopeGuardBlock(Writer w) => w.Unindent().WriteLine("});");

    public static string EmitObjDropImplScopeGuard(Module module, Writer w, TempVarNameGen nameGen, string objName)
    {
        var scopeGuardName = nameGen.CreateNext();
        w.Write($"const auto {scopeGuardName} = ");
        BeginScopeGuardBlock(module, w);
        w.Write($"{objName}.DropImpl();");
        EndScopeGuardBlock(w);
        w.WriteLine(';');
        return scopeGuardName;
    }

    [Flags]
    private enum GenerateArgumentsFlags
    {
        None = 0,
        WithOutArrayParams = 1,
        WithDelgSysValues = 2,
        PassFuncPtrsAsNull = 4
    }
}