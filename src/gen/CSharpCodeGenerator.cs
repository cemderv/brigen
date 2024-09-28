using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

public sealed class CSharpCodeGenerator(Module module) : CodeGenerator(module)
{
    [Flags]
    public enum GetTypeNameFlags
    {
        None = 0,
        OutParam = 1,
        NoArray = 2,
        NoRef = 4,
        NoOut = 8
    }

    public enum NameContext
    {
        General,
        DelegateReturnType,
        DelegateFuncParam,
        PInvokeParameter,
        PInvokeReturnType
    }

    private const string _intPtr = "IntPtr";
    internal const string IntPtrFieldName = "_ptr";

    private static readonly Dictionary<IDataType, string> primTypeNameMap = new()
  {
    { PrimitiveType.Byte, "byte" },
    { PrimitiveType.Int, "int" },
    { PrimitiveType.Short, "short" },
    { PrimitiveType.Long, "long" },
    { PrimitiveType.Bool, "bool" },
    { PrimitiveType.Float, "float" },
    { PrimitiveType.Double, "double" },
    { PrimitiveType.String, "string" },
    { PrimitiveType.Void, "void" },
    { PrimitiveType.Handle, "IntPtr" }
  };

    private readonly TempVarNameGen _nameGen = new("brigen_");

    public override void Generate()
    {
        Logger.LogCategoryGenerationStatus("C#");
        Logger.LogFileGenerationStatus("P/Invoke File", Module.Paths.CSharpFile);

        Directory.CreateDirectory(Path.GetDirectoryName(Module.Paths.CSharpFile)!);

        var w = new Writer();

        w.WriteAutoGenerationNotice(Module.Paths.CSharpFile, [$"This is the C# wrapper file for {Module.Name}."],
          "//", false);

        w.WriteLine("// ReSharper disable InconsistentNaming");
        w.WriteLine("// ReSharper disable UnusedType.Global");
        w.WriteLine("// ReSharper disable FieldCanBeMadeReadOnly.Global");
        w.WriteLine("// ReSharper disable ArrangeMethodOrOperatorBody");
        w.WriteLine("// ReSharper disable UnusedMember.Global");
        w.WriteLine();

        w.WriteLine("using System;");
        w.WriteLine("using System.Diagnostics;");
        w.WriteLine("using System.Runtime.InteropServices;");

        w.WriteLine();

        BeginNamespace(w);

        foreach (Decl decl in Module.Decls)
        {
            w.ClearWriteMarker();

            if (decl is EnumDecl enumDecl)
                GenerateEnum(w, enumDecl);
            else if (decl is StructDecl structDecl)
                GenerateStruct(w, structDecl);
            else if (decl is ClassDecl classDecl)
                GenerateClass(w, classDecl);
            else if (decl is DelegateDecl delgDecl)
                GenerateDelegate(w, delgDecl);

            if (w.HasWrittenAnything && decl != Module.Decls[^1])
                w.WriteLine();
        }

        w.WriteLine();
        GenerateNativeFunctionsClass(w);

        EndNamespace(w);

        w.SaveContentsToDisk(Module.Paths.CSharpFile);
    }

    public static string GetTypeName(IDataType type, NameContext context, GetTypeNameFlags flags)
    {
        var arrayType = type as ArrayType;
        if (arrayType != null)
            type = arrayType.ElementType;

        if (type.IsUserDefined)
        {
            if (context is NameContext.PInvokeParameter or NameContext.PInvokeReturnType)
            {
                if (type.IsClass)
                    return _intPtr;

                if (type.IsDelegate)
                    return type.Name;

                string prefix = string.Empty;

                if (flags.HasFlag(GetTypeNameFlags.OutParam))
                {
                    if (!flags.HasFlag(GetTypeNameFlags.NoOut))
                        prefix += "out ";
                }
                else if (type is not EnumDecl && !flags.HasFlag(GetTypeNameFlags.NoRef))
                {
                    prefix += "ref ";
                }

                return prefix + type.Name;
            }

            string typeName = type.Name;

            if (arrayType != null && !flags.HasFlag(GetTypeNameFlags.NoArray))
                typeName += "[]";

            return typeName;
        }
        else
        {
            bool shouldAppendArray = arrayType != null && !flags.HasFlag(GetTypeNameFlags.NoArray);

            string typeName = primTypeNameMap[type];

            //if (type == PrimitiveType.String)
            //  typeName = _intPtr;

            if (shouldAppendArray)
                typeName += "[]";

            return typeName;
        }
    }

    private void GenerateEnum(Writer w, EnumDecl enm)
    {
        GenerateComment(w, enm.Comment, enm);

        if (enm.IsFlags)
            w.WriteLine("[Flags]");

        w.WriteLine($"public enum {enm.Name}");
        w.OpenBrace();

        foreach (EnumMemberDecl member in enm.Members)
        {
            GenerateComment(w, enm.Comment, member);

            w.WriteLine($"{member.Name} = {member.Value!.Value},");
        }

        w.CloseBrace();
    }

    private void GenerateClass(Writer w, ClassDecl clss)
    {
        GenerateComment(w, clss.Comment, clss);

        if (clss.IsStatic)
        {
            w.WriteLine($"public static class {clss.Name}");
            w.OpenBrace();
            GenerateClassMembers(w, clss);
            w.CloseBrace();
        }
        else
        {
            w.WriteLine($"public struct {clss.Name} : IDisposable");
            w.OpenBrace();

            w.WriteLine("[DebuggerBrowsable(DebuggerBrowsableState.Never)]");
            w.WriteLine($"internal {_intPtr} {IntPtrFieldName};");
            w.WriteLine();

            // Default (internal) constructor
            {
                w.WriteLine($"public {clss.Name}({_intPtr} ptr)");
                w.OpenBrace();
                w.WriteLine($"{IntPtrFieldName} = ptr;");
                w.CloseBrace();
                w.WriteLine();
            }

            GenerateClassMembers(w, clss);

            // IDisposable implementation
            {
                string releaseFuncName = NameResolution.GetSpecialCApiFuncName(SpecialCApiFunc.Release, Module);

                w.WriteLine("public void Dispose()");
                w.OpenBrace();
                w.WriteLine($"if ({IntPtrFieldName} != IntPtr.Zero)");
                w.OpenBrace();

                string classNameInC = CCodeGenerator.GetTypeName(clss, CCodeGenerator.NameContext.Neutral);

                w.WriteLine($"NativeFunctions.{classNameInC}_{releaseFuncName}({IntPtrFieldName});");
                w.WriteLine($"{IntPtrFieldName} = IntPtr.Zero;");
                w.CloseBrace();
                w.CloseBrace();
                w.WriteLine();
            }

            // Classes ever only have one field: the pointer.
            string[] fields = [IntPtrFieldName];

            GenerateEqualsOverride(w, clss.Name, fields);
            w.WriteLine();

            GenerateGetHashCodeOverride(w, fields);
            w.WriteLine();

            GenerateComparisonOperators(w, clss.Name);
            w.WriteLine();

            // VerifyClas()
            {
                w.WriteLine("[Conditional(\"DEBUG\")]");
                w.WriteLine("private void VerifyClass()");
                w.OpenBrace();
                w.WriteLine($"if ({IntPtrFieldName} == IntPtr.Zero)");
                w.OpenBrace();
                w.WriteLine("throw new InvalidOperationException(");
                w.WriteLine(
                  $"\"The {clss.Name} instance is not valid. This is an indication that the instance is not initialized or that it has been disposed.\");");
                w.CloseBrace();
                w.CloseBrace();
            }
        }

        w.CloseBrace();
    }

    private void GenerateStruct(Writer w, StructDecl strct)
    {
        GenerateComment(w, strct.Comment, strct);

        string name = strct.Name;

        w.WriteLine("[StructLayout(LayoutKind.Sequential)]");
        w.WriteLine($"public struct {name} : IEquatable<{name}>");

        w.OpenBrace();

        // Fields
        foreach (StructFieldDecl field in strct.Fields)
        {
            GenerateComment(w, field.Comment, field);

            string typeName = GetTypeName(field.Type, NameContext.General, GetTypeNameFlags.None);

            w.WriteLine($"public {typeName} {field.Name};");
        }

        w.WriteLine();

        // Constructors
        {
            // Signature
            w.Write($"public {name}(");
            foreach (StructFieldDecl field in strct.Fields)
            {
                string fieldType = GetTypeName(field.Type, NameContext.General, GetTypeNameFlags.None);
                string fieldName = field.Name.Cased(CaseStyle.CamelCase);

                w.Write($"{fieldType} {fieldName}");

                if (field != strct.Fields[^1])
                    w.Write(", ");
            }

            w.WriteLine(")");

            // Body
            w.OpenBrace();
            foreach (StructFieldDecl field in strct.Fields)
            {
                string fieldName = field.Name;
                string paramName = fieldName.Cased(CaseStyle.CamelCase);

                // Distinquish field from parameter if they have the same name.
                if (fieldName == paramName) w.Write("this.");

                w.WriteLine($"{fieldName} = {paramName};");
            }

            w.CloseBrace();
        }

        w.WriteLine();

        List<string> fieldNames = strct.Fields.Select(f => f.Name).ToList();

        GenerateEqualsOverride(w, name, fieldNames);
        w.WriteLine();

        GenerateGetHashCodeOverride(w, fieldNames);
        w.WriteLine();

        GenerateStructToStringOverride(w, strct);
        w.WriteLine();

        GenerateComparisonOperators(w, name);

        w.CloseBrace();
    }

    private void GenerateDelegate(Writer w, DelegateDecl delg)
    {
        string returnTypeName = GetTypeName(delg.ReturnType!, NameContext.DelegateReturnType, GetTypeNameFlags.None);

        w.WriteLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");

        w.Write($"public delegate {returnTypeName} {delg.Name}(");

        foreach (FunctionParamDecl param in delg.Parameters)
        {
            // Before we can write the parameter type etc., we have to see
            // if this parameter requires any special marshaling attributes
            // ([MarshalAs(...)]).
            {
                var stringVec = new List<string>();

                if (param.Type.IsArray)
                {
                    stringVec.Add("UnmanagedType.LPArray");

                    if (param.Type == PrimitiveType.String) stringVec.Add("ArraySubType = UnmanagedType.LPStr");

                    stringVec.Add($"SizeParamIndex = {param.IndexInCApi + 1}");
                }

                if (stringVec.Count > 0)
                {
                    w.Write("[MarshalAs(");
                    foreach (string mp in stringVec)
                    {
                        w.Write(mp);
                        if (mp != stringVec[^1])
                            w.Write(", ");
                    }

                    w.Write(")] ");
                }
            }

            string typeName = GetTypeName(param.Type, NameContext.DelegateFuncParam, GetTypeNameFlags.None);

            w.Write($"{typeName} {param.Name}");

            if (param.Type.IsArray)
                w.Write($", uint {param.Name}Size");

            if (param != delg.Parameters[^1])
                w.Write(", ");
        }

        w.WriteLine(");");
    }

    private void GenerateClassMembers(Writer w, ClassDecl clss)
    {
        // Constructors first
        foreach (FunctionDecl function in clss.Ctors)
        {
            _nameGen.Clear();

            GenerateComment(w, function.Comment, clss);

            w.Write($"public static {clss.Name} {function.NameInCSharp}(");
            GenerateMethodParameters(w, function);
            w.WriteLine(")");
            w.OpenBrace();
            GeneratePInvokeFunctionCall(w, function);
            w.CloseBrace();

            w.WriteLine();
        }

        w.WriteLine();

        // Normal methods
        foreach (FunctionDecl function in clss.Functions)
        {
            _nameGen.Clear();

            GenerateComment(w, function.Comment, function);

            string returnType = GetTypeName(function.ReturnType, NameContext.General, GetTypeNameFlags.None);

            w.Write("public ");

            if (function.IsStatic)
                w.Write("static ");

            w.Write($"{returnType} {function.Name}(");
            GenerateMethodParameters(w, function);
            w.WriteLine(')');
            w.OpenBrace();

            if (!function.IsStatic)
                WriteClassPtrAssertion(w);

            bool hasUnsafeBlock = ShouldFuncBodyBeWrappedInUnsafeBlock(function);

            if (hasUnsafeBlock)
            {
                w.WriteLine("unsafe");
                w.OpenBrace();
            }

            GeneratePInvokeFunctionCall(w, function);

            if (hasUnsafeBlock) w.CloseBrace();

            w.CloseBrace();
            w.WriteLine();
        }

        // Properties
        foreach (PropertyDecl prop in clss.Properties)
        {
            _nameGen.Clear();

            string returnType = GetTypeName(prop.Type, NameContext.General, GetTypeNameFlags.None);

            GenerateComment(w, prop.Comment, prop);

            w.Write("public ");

            if (prop.IsStatic)
                w.Write("static ");

            w.WriteLine($"{returnType} {prop.Name}");
            w.OpenBrace();

            if (prop.HasGetter)
            {
                FunctionDecl func = clss.GetFunctionForProperty(prop, PropertyDecl.PropMask.Getter) ?? throw CompileError.Internal(
                      $"Property {prop.Name}: could not obtain respective getter function.",
                      prop.Range);

                w.WriteLine("get");
                w.OpenBrace();

                if (!prop.IsStatic)
                    WriteClassPtrAssertion(w);

                bool hasUnsafeBlock = ShouldFuncBodyBeWrappedInUnsafeBlock(func);

                if (hasUnsafeBlock)
                {
                    w.WriteLine("unsafe");
                    w.OpenBrace();
                }

                GeneratePInvokeFunctionCall(w, func);

                if (hasUnsafeBlock)
                    w.CloseBrace();

                w.CloseBrace();
            }

            if (prop.HasSetter)
            {
                FunctionDecl func = clss.GetFunctionForProperty(prop, PropertyDecl.PropMask.Setter) ?? throw CompileError.Internal(
                      $"Property {prop.Name}: could not obtain respective setter function.",
                      prop.Range);

                w.WriteLine("set");
                w.OpenBrace();

                if (!prop.IsStatic)
                    WriteClassPtrAssertion(w);

                bool hasUnsafeBlock = ShouldFuncBodyBeWrappedInUnsafeBlock(func);

                if (hasUnsafeBlock)
                {
                    w.WriteLine("unsafe");
                    w.OpenBrace();
                }

                GeneratePInvokeFunctionCall(w, func);

                if (hasUnsafeBlock)
                    w.CloseBrace();

                w.CloseBrace();
            }

            w.CloseBrace();
            w.WriteLine();
        }
    }

    private void GeneratePInvokeFunctionCall(Writer w, FunctionDecl function)
    {
        var funcCaller = new CSharpPInvokeFuncCaller(w, _nameGen, function);
        funcCaller.GenerateCall();
    }

    private void GenerateMethodParameters(Writer w, FunctionDecl function)
    {
        foreach (FunctionParamDecl param in function.Parameters)
        {
            string typeName = GetTypeName(param.Type, NameContext.General, GetTypeNameFlags.None);

            w.Write($"{typeName} {param.Name}");

            if (param != function.Parameters[^1])
                w.Write(", ");
        }
    }

    private void GenerateEqualsOverride(Writer w, string name, IEnumerable<string> fields)
    {
        w.WriteLine($"public bool Equals({name} other)");
        w.OpenBrace();
        w.Write("return");

        if (fields.Count() > 1)
            w.WriteLine();
        else
            w.Write(" ");

        w.Indent();
        int i = 0;
        foreach (string field in fields)
        {
            w.Write($"{field} == other.{field}");

            if (i + 1 < fields.Count())
                w.Write(" &&");
            else
                w.Write(';');

            w.WriteLine();
            ++i;
        }

        w.Unindent();
        w.CloseBrace();

        w.WriteLine();

        bool usingNullRefs = Module.GetBoolVariable(VariableNames.CSharpNullRef, false);

        w.Write($"public override bool Equals(object");

        if (usingNullRefs)
            w.Write("?");

        w.WriteLine($" obj)");

        w.OpenBrace();
        w.WriteLine($"return obj is {name} other && this == other;");
        w.CloseBrace();
    }

    private void GenerateGetHashCodeOverride(Writer w, IEnumerable<string> fields)
    {
        w.WriteLine("public override int GetHashCode()");
        w.OpenBrace();

#if false
  constexpr int hashCodeBatchSize = 8u;

  if (fields.Count() < hashCodeBatchSize)
  {
    w.Write( "return HashCode.Combine(";
    int i = 0u;
    foreach (var field in fields)
    {
      w.Write(field);

      if ((i + 1) < fields.Count())
        w.Write(", ");

      ++i;
    }
    w.Write( ");\n";
  }
  else
  {
    const int howManyBatches =
        int(ceil(double(fields.Count()) / hashCodeBatchSize));

    foreach (int b = 0; b < howManyBatches; ++b)
    {
      const int offset = b * hashCodeBatchSize;

      w.Write( "int hash" << to_string(b) << " = HashCode.Combine(";

      const int howManyFields = min(fields.Count(), hashCodeBatchSize);

      int i = 0;
      foreach (int f = 0; f < howManyFields; ++f)
      {
        w.Write( fields[offset + f];

        if ((i + 1) < howManyFields)
          w.Write( ", ";

        ++i;
      }

      w.Write( ");\n";
    }

    w.Write("return HashCode.Combine(");

    foreach (int b = 0; b < howManyBatches; ++b)
    {
      w.Write( "hash" << to_string(b);

      if ((b + 1) < howManyBatches)
        w.Write( ", ";
    }

    w.Write( ");\n";
  }
#else
        w.WriteLine("unchecked");
        w.OpenBrace();
        w.WriteLine($"int hash = {Module.HashFirstPrime};");
        foreach (string field in fields)
            w.WriteLine($"hash = (hash * {Module.HashSecondPrime}) + {field}.GetHashCode();");

        w.WriteLine("return hash;");
        w.CloseBrace();
#endif

        w.CloseBrace();
    }

    private void GenerateStructToStringOverride(Writer w, StructDecl strct)
    {
        if (!strct.Fields.Any())
            return;

        w.WriteLine("public override string ToString()");
        w.OpenBrace();

        if (strct.Fields.Count > 1u)
        {
            w.WriteLine("var sb = new System.Text.StringBuilder(32);");

            const int maxFields = 4;
            int howManyFields = Math.Min(strct.Fields.Count, maxFields);

            for (int i = 0; i < howManyFields; ++i)
            {
                StructFieldDecl field = strct.Fields.ElementAt(i);

                w.WriteLine($"sb.Append(\"{field.Name}=\");");
                w.WriteLine($"sb.Append({field.Name});");

                if (i + 1 < howManyFields)
                    w.WriteLine("sb.Append(\"; \");");
            }

            w.WriteLine("return sb.ToString();");
        }
        else
        {
            Debug.Assert(strct.Fields.Count == 1u);
            w.WriteLine($"return {strct.Fields[0].Name}.ToString();");
        }

        w.CloseBrace();
    }

    private void GenerateComparisonOperators(Writer w, string name)
    {
        w.WriteLine($"public static bool operator==({name} lhs, {name} rhs)");
        w.OpenBrace();
        w.WriteLine("return lhs.Equals(rhs);");
        w.CloseBrace();

        w.WriteLine();

        w.WriteLine($"public static bool operator!=({name} lhs, {name} rhs)");
        w.OpenBrace();
        w.WriteLine("return !lhs.Equals(rhs);");
        w.CloseBrace();
    }

    private void GenerateComment(Writer w, CommentDecl? comment, Decl parentDecl)
    {
        if (comment == null)
            return;

#if false
      if (string.IsNullOrEmpty(comment))
        return;

      w.WriteLine("/// <summary>");

      string[] lines = comment.Split('\n');

      foreach (string line in lines)
      {
        w.Write("/// ");

        if (parentDecl is PropertyDecl prop && line == lines.First())
        {
          if (prop.HasGetter && prop.HasSetter)
            w.Write("Gets or sets ");
          else if (prop.HasGetter)
            w.Write("Gets ");
          else if (prop.HasSetter) w.Write("Sets ");
        }

        w.Write(line);
        w.WriteLine();
      }

      w.WriteLine("/// </summary>");

#if false
      foreach (var pair in comment.getParameterDescriptions())
      {
        w.Write("/// <param name=\"" << pair.first << "\">" << pair.second << "</param>\n";
      }
#endif
#endif
    }

    private void GenerateNativeFunctionsClass(Writer w)
    {
        w.WriteLine("internal static class NativeFunctions");
        w.OpenBrace();

        w.WriteLine($"const string LibName = \"{Module.Name}\";");
        w.WriteLine();

        foreach (FunctionDecl function in Module.AllExportedFunctions)
        {
            GeneratePInvokeFunctionSignature(w, function);

            if (function != Module.AllExportedFunctions[^1])
                w.WriteLine();
        }

        w.WriteLine();

        // Destructor functions
        foreach (ClassDecl clss in Module.Decls.OfType<ClassDecl>())
        {
            GenerateDllImportAttribute(w, null);

            string releaseFuncName = NameResolution.GetSpecialCApiFuncName(SpecialCApiFunc.Release, Module);
            string classNameInC = CCodeGenerator.GetTypeName(clss, CCodeGenerator.NameContext.Neutral);

            w.WriteLine(
              $"internal static extern void {classNameInC}_{releaseFuncName}(IntPtr {Strings.CSharpPInvokeThisParamName});");
            w.WriteLine();
        }

        w.CloseBrace();
    }

    private string GetFunctionReturnTypeForPInvoke(FunctionDecl function)
    {
        IDataType? returnType = function.ReturnType;

        if (function.IsCtor) return _intPtr;

        if (returnType is { IsClass: true, IsArray: false })
            return _intPtr;

        if (function.HasNonVoidReturnType)
        {
            if (function.HasOutReturnParam) return "void";

            Debug.Assert(returnType != null);

            if (returnType == PrimitiveType.String)
                return _intPtr;

            if (returnType == PrimitiveType.Bool)
                return "int";

            return GetTypeName(returnType, NameContext.PInvokeReturnType, GetTypeNameFlags.None);
        }

        return "void";
    }

    private void GeneratePInvokeFunctionSignature(Writer w, FunctionDecl function)
    {
        string returnTypeName = GetFunctionReturnTypeForPInvoke(function);

        // Header
        GenerateDllImportAttribute(w, function);
        w.Write($"public static extern {returnTypeName} {function.NameInC}(");

        bool haveWrittenAnything = false;

        // This-parameter
        if (function.HasThisParam)
        {
            w.Write($"IntPtr {Strings.CSharpPInvokeThisParamName}");

            if (function.Parameters.Any())
                w.Write(", ");

            haveWrittenAnything = true;
        }

        // Parameters
        foreach (FunctionParamDecl param in function.Parameters)
        {
            IDataType paramType = param.Type;
            var paramArrayType = paramType as ArrayType;

            string typeName = GetTypeName(paramType, NameContext.PInvokeParameter, GetTypeNameFlags.None);

            if (paramArrayType != null)
            {
                if (paramType == PrimitiveType.String)
                {
                    int arraySizeIndex = param.IndexInCApi + 1;
                    if (function.HasThisParam)
                        ++arraySizeIndex;

                    w.Write(
                      $"[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr, SizeParamIndex = {arraySizeIndex})] ");

                    typeName = "string[]";
                }
                else
                {
                    typeName = _intPtr;
                }
            }

            string name = param.Name;

            w.Write($"{typeName} {name}");

            if (paramArrayType != null)
                w.Write($", uint {param.Name}Size");

            if (param != function.Parameters[^1])
                w.Write(", ");

            haveWrittenAnything = true;
        }

        // Handle eventual out-parameter.
        if (function.HasOutReturnParam)
        {
            if (haveWrittenAnything)
                w.Write(", ");

            if (function.ReturnType.IsArray)
            {
                w.Write(
                  $"IntPtr {Strings.CSharpPInvokeResultArrayName}, IntPtr {Strings.CSharpPInvokeResultArraySizeName}");
            }
            else
            {
                string typeName =
                  GetTypeName(function.ReturnType, NameContext.PInvokeParameter, GetTypeNameFlags.OutParam);
                w.Write($"{typeName} {Strings.CSharpPInvokeResultName}");
            }
        }

        w.WriteLine(");");
    }

    private void GenerateDllImportAttribute(Writer w, FunctionDecl? function)
    {
        w.Write("[DllImport(LibName, ");
        w.Write("CallingConvention = CallingConvention.Cdecl, ");

        var stringVec = new List<string>();

        if (function != null)
        {
            bool containsAnyStringParams = function.Parameters.Any(p => p.Type == PrimitiveType.String);

            if (containsAnyStringParams)
                stringVec.Add("CharSet = CharSet.Ansi");
        }

        stringVec.Add("ExactSpelling = true");

        foreach (string param in stringVec)
        {
            w.Write(param);
            if (param != stringVec[^1])
                w.Write(", ");
        }

        w.WriteLine(")]");
    }

    private bool ShouldFuncBodyBeWrappedInUnsafeBlock(FunctionDecl function)
    {
        // Functions that return arrays require us to pin the managed array
        // so that it can be filled from the native side.
        if (function.HasNonVoidReturnType && function.ReturnType.IsArray)
            return true;

        return false;
    }

    private void WriteClassPtrAssertion(Writer w) => w.WriteLine("VerifyClass();");

    private void BeginNamespace(Writer w)
    {
        w.WriteLine($"namespace {Module.Name}");
        w.OpenBrace();
    }

    private void EndNamespace(Writer w) => w.CloseBrace();
}