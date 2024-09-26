using brigen.decl;
using brigen.types;
using System.Text;

namespace brigen.gen;

public sealed class CCodeGenerator : NativeCodeGenerator
{
    private const string CBoolTypeName = "cbool32_t";

    private static readonly Dictionary<IDataType, string> _primTypeNameTable = new()
  {
    { PrimitiveType.Byte, "uint8_t" },
    { PrimitiveType.Int, "int32_t" },
    { PrimitiveType.Short, "int16_t" },
    { PrimitiveType.Long, "int64_t" },
    { PrimitiveType.Bool, CBoolTypeName },
    { PrimitiveType.Float, "float" },
    { PrimitiveType.Double, "double" },
    { PrimitiveType.String, "const char*" },
    { PrimitiveType.Handle, "void*" },
    { PrimitiveType.Void, "void" },
  };

    private readonly TempVarNameGen _nameGen;

    public CCodeGenerator(Module module)
      : base(module)
    {
        _nameGen = new TempVarNameGen(Strings.ForbiddenIdentifierPrefix);

        Directory.CreateDirectory(Path.GetDirectoryName(module.Paths.CHeader)!);
        Directory.CreateDirectory(Path.GetDirectoryName(module.Paths.CSource)!);
    }

    public override void Generate()
    {
        Logger.LogCategoryGenerationStatus("C");

        GenerateHeaderFile();
        GenerateSourceFile();
    }

    private void GenerateHeaderFile()
    {
        Logger.LogFileGenerationStatus("Header", Module.Paths.CHeader);

        var w = new Writer();

        {
            string line = $"This is the header file for the C API of {Module.Name}.";
            w.WriteAutoGenerationNotice(Module.Paths.CHeader, [line], "//", false);
        }

        w.WriteLine("// clang-format off");

        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        BeginCHeader(w);

        w.ClearWriteMarker();

        // Forward declarations
        {
            IOrderedEnumerable<TypeDecl>? decls = Module.Decls
              .Where(d => d is StructDecl or ClassDecl)
              .OfType<TypeDecl>()
              .OrderBy(d => d.Name);

            foreach (TypeDecl decl in decls)
            {
                GenerateComment(w, decl.Comment, decl);
                string name = GetTypeName(decl, NameContext.Neutral);
                w.WriteLine($"typedef struct {name} {name};");
            }
        }

        foreach (TypeDecl typeDecl in Module.AllTypeDecls(true))
        {
            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            if (typeDecl is EnumDecl enumDecl)
                GenerateEnum(w, enumDecl);
            else if (typeDecl is StructDecl structDecl)
                GenerateStruct(w, structDecl);
            else if (typeDecl is ClassDecl classDecl)
                GenerateClass(w, classDecl);
            else if (typeDecl is DelegateDecl delgDecl)
                GenerateDelegate(w, delgDecl);
            else
                throw new CompileError("Invalid type decl", typeDecl.Range);
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        EndCHeader(w);

        w.SaveContentsToDisk(Module.Paths.CHeader);
    }

    private void GenerateSourceFile()
    {
        Logger.LogFileGenerationStatus("Source", Module.Paths.CSource);

        string moduleName = Module.Name;

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CSource,
          [$"This is the source file for the C API of {moduleName}."], "//", false);

        w.WriteLine("// clang-format off");
        w.WriteLine();

        w.WriteLine($"#include \"{moduleName}.h\"");
        w.WriteLine();
        w.WriteLine($"#include \"{moduleName}.hpp\"");
        w.WriteLine($"#include \"{moduleName}_impl.hpp\"");
        w.WriteLine();

        // ClassBuffer
        {
            w.Write($"namespace {Strings.CppInternalNamespace} ");
            w.WriteLine('{');
            w.WriteLine(Properties.Resources.TS_CClassBuffer);
            w.WriteLine('}');
            w.WriteLine();
        }

        foreach (ClassDecl clss in Module.Decls.OfType<ClassDecl>())
        {
            var ctors = clss.Ctors;

            // Constructors
            foreach (FunctionDecl ctor in ctors)
            {
                GenerateCApiFunctionSignature(w, ctor, false);
                GenerateCApiFunctionBody(w, ctor);

                if (ctor != ctors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            if (!clss.IsStatic)
            {
                // AddRef() function
                GenerateCApiAddRefSignature(w, clss, false);
                GenerateCApiAddRefBody(w, clss);

                w.WriteLine();

                // Release() function
                GenerateCApiReleaseSignature(w, clss, false);
                GenerateCApiReleaseBody(w, clss);
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // Functions
            foreach (FunctionDecl func in clss.AllExportedFunctionsWithoutCtors)
            {
                GenerateCApiFunctionSignature(w, func, false);
                GenerateCApiFunctionBody(w, func);

                if (func != clss.AllExportedFunctionsWithoutCtors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();
        }

        w.SaveContentsToDisk(Module.Paths.CSource);
    }


    private void GenerateCApiFunctionBody(Writer w, FunctionDecl function)
    {
        _nameGen.Clear();

        w.OpenBrace();

        var funcCaller = new CToCppFuncCaller(w, _nameGen, function);
        funcCaller.GenerateCall();

        w.CloseBrace();
    }

    private void BeginCHeader(Writer w)
    {
        string includeGuardName = Module.UpperName + "_H_INCLUDED";

        w.WriteLine($"#ifndef {includeGuardName}");
        w.WriteLine($"#define {includeGuardName}");
        w.WriteLine();

        GenerateHeaderPreamble(w, false);

        w.WriteLine($"typedef uint32_t {CBoolTypeName};");
        w.WriteLine("#define bool32_true 1u");
        w.WriteLine("#define bool32_false 0u");
        w.WriteLine();

        w.WriteLine("#if defined(__cplusplus)");
        w.WriteLine("extern \"C\" {");
        w.WriteLine("#endif");
        w.WriteLine();
    }

    private void EndCHeader(Writer w)
    {
        w.WriteLine("#if defined(__cplusplus)");
        w.WriteLine("}");
        w.WriteLine("#endif");
        w.WriteLine();
        w.WriteLine($"#endif // {Module.UpperName}_H_INCLUDED");
        w.WriteLine();
    }

    /// <summary>
    /// Translates a type name into its equivalent in C.
    /// </summary>
    /// <param name="type">The type to translate.</param>
    /// <param name="context">The name context.</param>
    /// <returns></returns>
    public static string GetTypeName(IDataType type, NameContext context)
    {
        var arrayType = type as ArrayType;

        // See if we can determine a shortcut to type detection.
        if (context == NameContext.FunctionParam)
        {
            if (arrayType != null)
            {
                var elemType = arrayType.ElementType;
                if (elemType == PrimitiveType.String || elemType == PrimitiveType.Handle)
                    return $"{_primTypeNameTable[elemType]} const*";
            }
            else
            {
                if (type == PrimitiveType.String || type == PrimitiveType.Handle)
                    return _primTypeNameTable[type];
            }
        }
        else if (context == NameContext.Neutral)
        {
            if (type is TypeDecl typeDecl)
                return $"{typeDecl.Module!.Name}_{typeDecl.Name}";
        }

        const int fNone = 0;
        const int fConst = 1;
        const int fPointer = 2;

        int flags = fNone;

        if (arrayType != null && context == NameContext.FunctionParam)
        {
            flags |= fConst;
            flags |= fPointer;
        }

        if (type.IsStruct)
        {
            if (context != NameContext.StructField)
            {
                flags |= fConst;
                flags |= fPointer;
            }
        }
        else if (type.IsClass)
        {
            flags &= ~fConst;
            flags |= fPointer;
        }
        else if (type.IsDelegate)
        {
            // Delegates are passed by value as function parameters, since their typedef
            // already contains the pointer.
            flags = fNone;
        }

        if (context == NameContext.OutParam)
        {
            flags &= ~fConst;

            if (arrayType != null)
                flags |= fPointer;
        }

        // If neutral type name was requested, remove all modifiers.
        if (context == NameContext.Neutral)
            flags = fNone;

        var ret = new StringBuilder();

        var baseType = arrayType != null ? arrayType.ElementType : type;
        if (_primTypeNameTable.TryGetValue(baseType, out string? primTypeName))
            ret.Append(primTypeName);
        else if (baseType is TypeDecl typeDecl)
            ret.Append($"{typeDecl.Module!.Name}_{typeDecl.Name}");
        else
            throw CompileError.Internal($"Unknown type '{baseType.Name}' during C generation");

        if ((flags & fConst) == fConst)
            ret.Insert(0, "const ");

        if ((flags & fPointer) == fPointer)
            ret.Append('*');

        if (context == NameContext.OutParam)
            if (arrayType != null && arrayType.ElementType.IsClass)
                ret.Append('*');

        return ret.ToString();
    }


    private void GenerateEnum(Writer w, EnumDecl enm)
    {
        GenerateComment(w, enm.Comment, enm);

        string enumTypeName = GetTypeName(enm, NameContext.Neutral);
        w.Write($"typedef enum {enumTypeName} ");
        w.OpenBrace();

        foreach (EnumMemberDecl member in enm.Members)
        {
            GenerateComment(w, member.Comment, member);
            w.WriteLine($"{enumTypeName}_{member.Name} = {member.Value!.Value},");
        }

        w.WriteLine($"{enumTypeName}_MAX_ENUM = 0x7FFFFFFF,");

        w.Unindent();
        w.WriteLine($"}} {enumTypeName};");
    }


    private void GenerateStruct(Writer w, StructDecl strct)
    {
        GenerateComment(w, strct.Comment, strct);

        string name = GetTypeName(strct, NameContext.Neutral);

        w.Write($"typedef struct {name} ");
        w.OpenBrace();

        foreach (StructFieldDecl field in strct.Fields)
        {
            string typeName = GetTypeName(field.Type, NameContext.StructField);
            w.Write($"{typeName} {field.Name}");
            w.WriteLine(";");
        }

        w.Unindent();
        w.WriteLine($"}} {name};");
    }

    private void GenerateClass(Writer w, ClassDecl clss)
    {
        string name = GetTypeName(clss, NameContext.Neutral);

        w.WriteLine("// -------------------------------------");
        w.WriteLine($"// {name} API");
        w.WriteLine("// -------------------------------------");
        w.WriteLine();

        w.ClearWriteMarker();

        // Constructors
        foreach (FunctionDecl ctor in clss.Ctors)
        {
            GenerateCApiFunctionSignature(w, ctor, true);
            if (ctor != clss.Ctors[^1])
                w.WriteLine();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        if (!clss.IsStatic)
        {
            // AddRef() function
            GenerateCApiAddRefSignature(w, clss, true);

            // Release() function
            GenerateCApiReleaseSignature(w, clss, true);
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Functions
        foreach (FunctionDecl function in clss.AllExportedFunctionsWithoutCtors)
        {
            GenerateCApiFunctionSignature(w, function, true);

            if (function != clss.AllExportedFunctionsWithoutCtors[^1])
                w.WriteLine();
        }
    }


    private void GenerateDelegate(Writer w, DelegateDecl delegateDecl)
    {
        if (delegateDecl.ReturnType == null)
            throw new NullReferenceException($"Delegate {delegateDecl.Name} does not have a valid return type.");

        string returnTypeName = GetTypeName(delegateDecl.ReturnType, NameContext.DelegateReturnType);

        w.Write($"typedef {returnTypeName}(*{Module.Name}_{delegateDecl.Name})");
        w.Write("(");

        foreach (FunctionParamDecl param in delegateDecl.Parameters)
        {
            string typeName = GetTypeName(param.Type, NameContext.FunctionParam);
            w.Write(typeName);

            w.Write(' ' + param.Name);

            if (param.Type.IsArray)
                w.Write($", uint32_t {param.Name}Size");

            if (param != delegateDecl.Parameters[^1])
                w.Write(", ");
        }

        w.WriteLine(");");
    }


    private void GenerateCApiFunctionSignature(Writer w, FunctionDecl function, bool inHeader)
    {
        if (function.IsCtor)
        {
            if (inHeader)
                w.Write(Module.DllExportApi + ' ');

            string returnTypeName = GetTypeName(function.ParentTypeDecl!, NameContext.Neutral);
            string cApiName = function.NameInC;

            w.Write(returnTypeName + "* ");

            if (inHeader)
                w.Write(Module.CallConvApi + ' ');

            w.Write(cApiName + '(');
            GenerateCApiParameters(w, function, inHeader);
            w.Write(")");
        }
        else
        {
            if (inHeader)
                w.Write(Module.DllExportApi + ' ');

            string returnType;

            if (function.HasOutReturnParam)
            {
                returnType = "void";
            }
            else
            {
                returnType = GetTypeName(function.ReturnType, NameContext.Neutral);
                if (function.ReturnType is ClassDecl or StructDecl)
                    returnType += '*';
            }

            w.Write(returnType + ' ');

            if (inHeader)
                w.Write(Module.CallConvApi + ' ');

            w.Write(function.NameInC).Write('(');
            GenerateCApiParameters(w, function, inHeader);
            w.Write(')');
        }

        if (inHeader)
            w.Write(";");

        w.WriteLine();
    }


    private void GenerateCApiAddRefSignature(Writer w, ClassDecl clss, bool inHeader)
    {
        if (inHeader)
            w.Write(Module.DllExportApi + ' ');

        string cApiName = GetTypeName(clss, NameContext.Neutral);
        string paramName = inHeader ? clss.Name.Cased(CaseStyle.CamelCase) : Strings.CApiThisParam;

        w.Write("uint32_t ");

        if (inHeader)
            w.Write(Module.CallConvApi + ' ');

        w.Write(
          $"{cApiName}_{NameResolution.GetSpecialCApiFuncName(SpecialCApiFunc.AddRef, Module)}({cApiName}* {paramName})");

        if (inHeader)
            w.Write(";");

        w.WriteLine();
    }


    private void GenerateCApiAddRefBody(Writer w, ClassDecl clss)
    {
        w.OpenBrace();

        _nameGen.Clear();

        string varName = _nameGen.CreateNext();

        w.Write($"if ({Strings.CApiThisParam} != nullptr) ");
        w.OpenBrace();
        w.WriteLine($"const auto {varName} = reinterpret_cast<{clss.QualifiedImplClassName}*>({Strings.CApiThisParam});");
        w.WriteLine($"return {varName}->AddRef();");
        w.CloseBrace();
        w.Write("else ");
        w.OpenBrace();
        w.WriteLine("return 0u;");
        w.CloseBrace();

        w.CloseBrace();
    }

    private void GenerateCApiReleaseSignature(Writer w, ClassDecl clss, bool inHeader)
    {
        if (inHeader)
            w.Write(Module.DllExportApi + ' ');

        string cApiName = GetTypeName(clss, NameContext.Neutral);
        string paramName = inHeader ? clss.Name.Cased(CaseStyle.CamelCase) : Strings.CApiThisParam;

        w.Write("uint32_t ");

        if (inHeader)
            w.Write(Module.CallConvApi).Write(' ');

        w.Write(
          $"{cApiName}_{NameResolution.GetSpecialCApiFuncName(SpecialCApiFunc.Release, Module)}({cApiName}* {paramName})");

        if (inHeader)
            w.Write(';');

        w.WriteLine();
    }

    private void GenerateCApiReleaseBody(Writer w, ClassDecl clss)
    {
        w.OpenBrace();

        _nameGen.Clear();

        string varName = _nameGen.CreateNext();

        w.Write($"if ({Strings.CApiThisParam} != nullptr) ")
          .OpenBrace()
          .WriteLine($"const auto {varName} = reinterpret_cast<{clss.QualifiedImplClassName}*>({Strings.CApiThisParam});")
          .WriteLine($"return {varName}->Release();")
          .CloseBrace()
          .Write("else ")
          .OpenBrace()
          .WriteLine("return 0u;")
          .CloseBrace()
          .CloseBrace();
    }

    private void GenerateCApiParameters(Writer w, FunctionDecl function, bool inHeader)
    {
        bool hasAnyParams = false;

        if (function.HasThisParam)
        {
            if (function.ParentTypeDecl == null)
            {
                throw CompileError.Internal($"Function {function.Name}: Does not have a parent type decl.", function.Range);
            }

            string className = GetTypeName(function.ParentTypeDecl!, NameContext.Neutral);
            string name = inHeader ? function.ParentTypeDecl.Name.Cased(CaseStyle.CamelCase) : Strings.CApiThisParam;

            if (function.IsConst)
                className = "const " + className;

            w.Write($"{className}* {name}");

            if (function.Parameters.Any() || function.HasOutReturnParam)
                w.Write(", ");

            hasAnyParams = true;
        }

        foreach (FunctionParamDecl param in function.Parameters)
        {
            string typeName = GetTypeName(param.Type, NameContext.FunctionParam);
            w.Write(typeName);

            w.Write(' ' + param.Name);

            if (param.Type.IsArray)
                w.Write($", uint32_t {param.Name}Size");

            hasAnyParams = true;

            if (param != function.Parameters[^1])
                w.Write(", ");
        }

        if (function.HasOutReturnParam)
        {
            w.WriteCommaSeparatorIfNotPresent();

            string typeName = GetTypeName(function.ReturnType, NameContext.OutParam);

            if (function.ReturnType.IsArray)
            {
                w.Write(typeName + " resultArray");
                w.Write(", uint32_t* resultArraySize");
            }
            else
            {
                w.Write(typeName + " result");
            }

            hasAnyParams = true;
        }

        if (!hasAnyParams)
            w.Write("void");
    }

    public enum NameContext
    {
        Neutral = 1,
        StructField,
        FunctionParam,
        OutParam,
        DelegateReturnType
    }
}