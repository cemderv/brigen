using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

/// <summary>
/// Handles Java code generation.
/// </summary>
public sealed class JavaCodeGenerator : CodeGenerator
{
    private const string JavaVmParamName = "vm";
    public const string CidsName = $"{Strings.ForbiddenIdentifierPrefix}cids";
    public const string FidsName = $"{Strings.ForbiddenIdentifierPrefix}fids";
    public const string MidsName = $"{Strings.ForbiddenIdentifierPrefix}mids";
    public const string CtorSuffix = "_ctor";

    private static readonly Dictionary<IDataType, (string NameInJni, string NameInJava)> _primTypeNameMap =
      new()
      {
      { PrimitiveType.Byte, ("jbyte", "byte") },
      { PrimitiveType.Int, ("jint", "int") },
      { PrimitiveType.Short, ("jshort", "short") },
      { PrimitiveType.Long, ("jlong", "long") },
      { PrimitiveType.Float, ("jfloat", "float") },
      { PrimitiveType.Double, ("jdouble", "double") },
      { PrimitiveType.Bool, ("jboolean", "boolean") },
      { PrimitiveType.Handle, ("jlong", "long") },
      { PrimitiveType.Void, ("void", "void") }
      };

    private readonly TempVarNameGen _nameGen;

    public JavaCodeGenerator(Module module)
      : base(module)
    {
        _nameGen = new TempVarNameGen(Strings.ForbiddenIdentifierPrefix);
    }

    public override void Generate()
    {
        GenerateAllJavaFiles();
        GenerateCppJniFile();
    }

    private void GenerateCppJniFile()
    {
        string dstPath = Module.Paths.JavaJNICppFile;

        var w = new Writer();

        w.WriteAutoGenerationNotice(dstPath, new[] { $"This is the JNI implementation file of {Module.Name}." },
          "//", false);

        w.WriteLine("// clang-format off");
        w.WriteLine();

        GenerateCppJniFileIncludes(w);

        w.WriteLine("static constexpr jint JNI_VERSION = JNI_VERSION_1_8;");
        w.WriteLine();

        GenerateCppJniClassIdsStruct(w);
        w.WriteLine();

        GenerateCppJniFieldIdsStruct(w);
        w.WriteLine();

        GenerateCppJniMethodIdsStruct(w);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        GenerateJObjectToStructConversionFunctions(w);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Serious stuff begins now.

        w.Write("extern \"C\" ");
        w.OpenBrace();

        GenerateJNI_onLoad(w);
        w.WriteLine();
        GenerateJNI_onUnload(w);
        w.WriteLine();
        GenerateCppJniFunctions(w);

        w.CloseBrace();

        w.SaveContentsToDisk(dstPath);
    }

    private void GenerateCppJniFileIncludes(Writer w)
    {
        var deps = new SortedSet<string>
    {
      Path.GetFileName(Module.Paths.CppImplHeader), Path.GetFileName(Module.Paths.CppHelpersHeader),
    };

        foreach (string dep in deps)
            w.WriteLine($"#include <{dep}>");

        w.WriteLine();

        deps = new SortedSet<string> { "jni.h", "vector", "string", "iostream" };

        foreach (string dep in deps)
            w.WriteLine($"#include <{dep}>");

        w.WriteLine();
    }

    private void GenerateCppJniClassIdsStruct(Writer w)
    {
        w.WriteLine($"static struct {CidsName}_t");
        w.OpenBrace();

        // Built-ins
        {
            w.WriteLine($"jclass {Strings.ForbiddenIdentifierPrefix}JavaString;");
        }

        // structs
        foreach (StructDecl strct in Module.AllStructs)
            w.WriteLine($"jclass {strct.Name};");

        // Classes
        foreach (ClassDecl clss in Module.AllClasses)
            w.WriteLine($"jclass {clss.Name};");

        w.Unindent();
        w.WriteLine($"}} {CidsName};");
    }

    private void GenerateCppJniFieldIdsStruct(Writer w)
    {
        w.WriteLine($"static struct {FidsName}_t");
        w.OpenBrace();

        // structs
        foreach (StructDecl strct in Module.AllStructs)
        {
            foreach (StructFieldDecl field in strct.Fields)
                w.WriteLine($"jfieldID {strct.Name}_{field.NameInJava};");
        }

        // Classes
        foreach (ClassDecl classes in Module.AllClasses)
            w.WriteLine($"jfieldID {classes.Name}_ptr;");

        w.Unindent();
        w.WriteLine($"}} {FidsName};");
    }

    private void GenerateCppJniMethodIdsStruct(Writer w)
    {
        w.WriteLine($"static struct {MidsName}_t");
        w.OpenBrace();

        // structs
        foreach (StructDecl strct in Module.AllStructs)
        {
            // Constructor
            w.WriteLine($"jmethodID {strct.Name}{CtorSuffix};");
        }

        // Classes
        foreach (ClassDecl clss in Module.AllClasses)
        {
            // Private constructor of the class has to be exposed to
            // the native code, because we're going to call it when returning
            // classes from native to Java.
            w.WriteLine($"jmethodID {clss.Name}{CtorSuffix};");
        }

        // TODO: delegates

        w.Unindent();
        w.WriteLine($"}} {MidsName};");
    }

    private void GenerateAllJavaFiles()
    {
        Logger.LogCategoryGenerationStatus("Java");

        string outDir = Module.Paths.JavaOutputDirectory;

        GenerateNativeExceptionJavaFile();

        List<(string, TypeDecl td)> allJavaFiles =
          Module.AllEnums.Select(e => (TypeDecl)e)
            .Concat(Module.AllStructs.Select(e => (TypeDecl)e))
            .Concat(Module.AllClasses.Select(e => (TypeDecl)e))
            .Concat(Module.AllDelegates.Select(e => (TypeDecl)e))
            .Select(td => (Path.Combine(outDir, td.Name + ".java").CleanPath(), td))
            .ToList();

        allJavaFiles.Sort((lhs, rhs) => string.Compare(lhs.Item1, rhs.Item1, StringComparison.Ordinal));

        foreach ((string filename, TypeDecl typeDecl) in allJavaFiles)
        {
            GenerateSingleJavaFile(filename, typeDecl);
        }
    }

    private void GenerateSingleJavaFile(string filename, TypeDecl typeDecl)
    {
        if (typeDecl is EnumDecl enm)
            GenerateEnum(filename, enm);
        else if (typeDecl is StructDecl strct)
            GenerateStruct(filename, strct);
        else if (typeDecl is ClassDecl clss)
            GenerateClass(filename, clss);
        else if (typeDecl is DelegateDecl delg)
            GenerateDelegate(filename, delg);
    }

    private void GenerateJObjectToStructConversionFunctions(Writer w)
    {
        const string conversionFuncName = $"{Strings.ForbiddenIdentifierPrefix}jobject_Convert";
        const string conversionFuncParams = "(JNIEnv* env, jobject obj)";

        w.WriteLine($"template <typename T> inline T {conversionFuncName}{conversionFuncParams}");
        w.OpenBrace();
        w.WriteLine("// Nothing to do here.");
        w.CloseBrace();

        w.WriteLine();

        foreach (StructDecl strct in Module.AllStructs)
        {
            string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
              CppCodeGenerator.NameContextFlags.ForceModulePrefix);

            _nameGen.Clear();

            w.WriteLine(
              $"template <> inline {structNameInCpp} {conversionFuncName}<{structNameInCpp}>{conversionFuncParams}");
            w.OpenBrace();

            string retVarName = _nameGen.CreateNext();
            w.WriteLine($"{structNameInCpp} {retVarName}{{}};");

            foreach (StructFieldDecl field in strct.Fields)
            {
                if (field.Type.IsUserDefined)
                {
                    var fieldTypeDecl = field.Type as TypeDecl;
                    Debug.Assert(fieldTypeDecl != null);

                    string objFieldVarName = _nameGen.CreateNext();
                    w.WriteLine(
                      $"const auto {objFieldVarName} = env->GetObjectField(obj, {FidsName}.{strct.Name}_{field.NameInJava});");

                    string typeRefStr = CppCodeGenerator.GetTypeName(Module, fieldTypeDecl, CppCodeGenerator.NameContext.Neutral,
                      CppCodeGenerator.NameContextFlags.ForceModulePrefix);
                    w.WriteLine($"{retVarName}.{field.Name} = {conversionFuncName}<{typeRefStr}>(env, {objFieldVarName});");
                }
                else
                {
                    w.Write($"{retVarName}.{field.Name} = ");

                    void EmitEnvMethodCall(string methodName)
                    {
                        w.Write($"env->{methodName}(obj, {FidsName}.{strct.Name}_{field.NameInJava})");
                    }

                    IDataType type = field.Type;

                    if (type == PrimitiveType.Byte)
                        EmitEnvMethodCall("GetByteField");
                    else if (type == PrimitiveType.Int)
                        EmitEnvMethodCall("GetIntField");
                    else if (type == PrimitiveType.Short)
                        EmitEnvMethodCall("GetShortField");
                    else if (type == PrimitiveType.Long)
                        EmitEnvMethodCall("GetLongField");
                    else if (type == PrimitiveType.Bool)
                        EmitEnvMethodCall("GetBooleanField");
                    else if (type == PrimitiveType.Float)
                        EmitEnvMethodCall("GetFloatField");
                    else if (type == PrimitiveType.Double)
                        EmitEnvMethodCall("GetDoubleField");
                    else if (type == PrimitiveType.String)
                        EmitEnvMethodCall("GetStringField");
                    else if (type == PrimitiveType.Handle)
                        w.Write($"reinterpret_cast<void*>(env->GetLongField(obj, {FidsName}.{strct.Name}_{field.NameInJava}))");
                    else
                        throw new NotImplementedException($"unknown JNI field type '{type.Name}'");

                    w.WriteLine(';');
                }
            }

            w.WriteLine($"return {retVarName};");
            w.CloseBrace();

            if (strct != Module.AllStructs[^1])
                w.WriteLine();
        }
    }

    private void GenerateEnum(string path, EnumDecl enm)
    {
        Logger.LogFileGenerationStatus(enm.Name, path);

        var w = new Writer();

        w.WriteAutoGenerationNotice(path, new[] { $"Enum {enm.Name}" }, "//", false);

        GeneratePackageDeclaration(w);

        w.Write($"public enum {enm.Name} ");
        w.OpenBrace();
        {
            foreach (EnumMemberDecl member in enm.Members)
            {
                Debug.Assert(member.Value.HasValue);

                w.Write($"{member.Name.AllUpperCasedIdentifier()}({member.Value.Value})");

                if (member != enm.Members[^1])
                    w.Write(", ");
            }

            w.WriteLine(';');
            w.WriteLine();

            w.Write($"{enm.Name}(int value) ");
            w.OpenBrace();
            {
                w.WriteLine("this.value = value;");
            }
            w.CloseBrace();

            w.WriteLine();
            w.Write("private int value;\n");
            w.WriteLine();

            w.Write("public int getValue() ");
            w.OpenBrace();
            {
                w.Write("return this.value;\n");
            }
            w.CloseBrace();
            w.WriteLine();

            w.Write("@Override\n");
            w.Write("public String toString() ");
            w.OpenBrace();
            {
                w.Write("return Integer.toString(value);\n");
            }
            w.CloseBrace();
        }
        w.CloseBrace();

        w.SaveContentsToDisk(path);
    }

    private void GenerateStruct(string path, StructDecl strct)
    {
        Logger.LogFileGenerationStatus(strct.Name, path);

        var w = new Writer();
        w.WriteAutoGenerationNotice(path, new[] { $"Struct {strct.Name}" }, "//", false);

        GeneratePackageDeclaration(w);

        w.ClearWriteMarker();

        w.Write($"public class {strct.Name} ");
        w.OpenBrace();

        foreach (StructFieldDecl field in strct.Fields)
        {
            w.Write($"public {field.Type.Name} {field.NameInJava};");
            w.WriteLine();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Ctors
        {
            // Default ctor
            w.Write($"public {strct.Name}() ");
            w.OpenBrace();
            w.WriteLine("// Default constructor");
            w.CloseBrace();

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // Ctor with fields as parameters.
            w.Write($"public {strct.Name}(");

            foreach (StructFieldDecl field in strct.Fields)
            {
                w.Write(GetTypeName(field.Type, NameContext.MethodParameter) + ' ' + field.NameInJava);

                if (field != strct.Fields[^1])
                    w.Write(", ");
            }

            w.Write(") ");
            w.OpenBrace();
            {
                foreach (StructFieldDecl field in strct.Fields)
                    w.WriteLine(string.Format("this.{0} = {0};", field.NameInJava));
            }
            w.CloseBrace();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        GenerateStructToStringOverride(w, strct);

        w.CloseBrace();

        w.SaveContentsToDisk(path);
    }

    private void GeneratePackageDeclaration(Writer w)
    {
        w.WriteLine($"package {Module.JavaPackageName};");
        w.WriteLine();
    }

    private void GenerateClass(string path, ClassDecl clss)
    {
        var w = new Writer();
        w.WriteAutoGenerationNotice(path, new[] { $"Class {clss.Name}" }, "//", false);

        GeneratePackageDeclaration(w);

        bool implementsAutoCloseable = clss.IsDisposable;

        w.Write($"public class {clss.Name} ");

        if (implementsAutoCloseable)
            w.Write("implements AutoCloseable ");

        w.OpenBrace();

        w.WriteLine("private long ptr;");
        w.WriteLine("private boolean isDisposed;");

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Ctors
        {
            // Private ctor
            {
                w.Write($"private {clss.Name}() ");
                w.OpenBrace();
                w.CloseBrace();
            }

            w.WriteLine();

            foreach (FunctionDecl ctor in clss.Ctors)
            {
                GenerateMethodForClassInJavaFile(w, ctor);
                if (ctor != clss.Ctors[^1])
                    w.WriteLine();
            }
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        // Functions
        {
            foreach (FunctionDecl func in clss.AllExportedFunctionsWithoutCtors)
            {
                GenerateMethodForClassInJavaFile(w, func);
                if (func != clss.AllExportedFunctionsWithoutCtors[^1])
                    w.WriteLine();
            }
        }

        // isDisposed()
        {
            w.WriteNewlineIfWrittenAnythingAndResetMarker();
            w.Write("public boolean isDisposed() ");
            w.OpenBrace();
            {
                w.WriteLine("return this.isDisposed;");
            }
            w.CloseBrace();
        }

        if (implementsAutoCloseable)
        {
            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            w.WriteLine("@Override");
            w.Write("public void close() ");
            w.OpenBrace();
            {
                w.Write("if (!this.isDisposed) ");
                w.OpenBrace();
                {
                    w.WriteLine("disposeNative();");
                    w.WriteLine("this.isDisposed = true;");
                }
                w.CloseBrace();
            }
            w.CloseBrace();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        GenerateNativeJavaMethodsForClass(w, clss);

        if (implementsAutoCloseable)
        {
            w.WriteNewlineIfWrittenAnythingAndResetMarker();
            w.WriteLine("private native void disposeNative();");
        }

        w.CloseBrace(); // end class

        w.SaveContentsToDisk(path);
    }

    private static void GenerateDelegate(string path, DelegateDecl delg)
    {
    }

    private void GenerateMethodForClassInJavaFile(Writer w, FunctionDecl function)
    {
        _nameGen.Clear();

        ClassDecl clss = function.ParentAsClass ?? throw new CompileError(
          $"Function {function.Name}: Does not have a parent class.",
          function.Range);

        w.Write("public ");

        if (function.IsCtor)
        {
            var funcCaller = new JavaToNativeFuncCaller(w, function);

            w.Write("static " + clss.Name + ' ' + function.NameInJava + '(');
            GenerateMethodParameters(w, function, NameContext.MethodParameter);
            w.Write(')');
            w.Write(" throws NativeException ");
            w.OpenBrace();
            string ptrName = _nameGen.CreateNext();
            w.Write("long " + ptrName + " = " + function.NativeNameInJava + '(');
            funcCaller.GenerateArguments();
            w.WriteLine(");");
            w.WriteLine($"if ({ptrName} == 0)");
            w.Indent();
            w.WriteLine("throw new NativeException(\"Failed to create the " + clss.Name + ".\");");
            w.Unindent();

            string retVarName = _nameGen.CreateNext();
            w.WriteLine($"{clss.Name} {retVarName} = new {clss.Name}();");
            w.WriteLine($"{retVarName}.ptr = {ptrName};");
            w.WriteLine($"return {retVarName};");

            w.CloseBrace();
        }
        else
        {
            if (function.IsStatic)
                w.Write("static ");

            w.Write(GetTypeName(function.ReturnType, NameContext.MethodReturnType) + ' ' + function.NameInJava +
                    '(');
            GenerateMethodParameters(w, function, NameContext.MethodParameter);
            w.Write(") ");
            w.OpenBrace();
            GenerateJavaMethodBody(w, function);
            w.CloseBrace();
        }
    }


    private void GenerateNativeJavaMethodsForClass(Writer w, ClassDecl clss)
    {
        foreach (FunctionDecl func in clss.AllExportedFunctions)
        {
            w.Write("private ");

            if (func.IsStatic || func.IsCtor)
                w.Write("static ");

            w.Write("native ");

            // Return-type
            w.Write(func.IsCtor ? "long" : GetTypeName(func.ReturnType, NameContext.NativeMethodReturnType));

            w.Write(' ' + func.NativeNameInJava + '(');
            GenerateMethodParameters(w, func, NameContext.NativeMethodParameter);
            w.WriteLine(");");

            if (func != clss.AllExportedFunctions[^1])
                w.WriteLine();
        }
    }


    private void GenerateJNI_onLoad(Writer w)
    {
        w.WriteLine($"JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* {JavaVmParamName}, void* reserved)");
        w.OpenBrace();

        w.WriteLine("JNIEnv* env = nullptr;");
        w.WriteLine($"if ({JavaVmParamName}->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION) != JNI_OK)");
        w.Indent();
        w.WriteLine("return JNI_ERR;");
        w.Unindent();
        w.WriteLine();

        w.WriteLine($"std::cout << \"Loading the native library of {Module.Name}.\" << std::endl;");
        w.WriteLine();

        GenerateJNIClassesExtraction(w);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        GenerateJNIMethodsExtraction(w);
        w.WriteNewlineIfWrittenAnythingAndResetMarker();
        GenerateJNIFieldsExtraction(w);

        // We have to return the JNI verson that we require so that the Java runtime
        // knows what to expect.
        w.WriteLine();
        w.WriteLine("return JNI_VERSION;");

        w.CloseBrace();
    }


    private void GenerateJNI_onUnload(Writer w)
    {
        w.WriteLine("JNIEXPORT void JNICALL JNI_OnUnload(JavaVM* vm, void* reserved)");
        w.OpenBrace();
        w.WriteLine("JNIEnv* env = nullptr;");
        w.WriteLine($"{JavaVmParamName}->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION);");

        w.WriteLine($"env->DeleteGlobalRef({CidsName}.{Strings.ForbiddenIdentifierPrefix}JavaString);");

        foreach (StructDecl strct in Module.AllStructs)
            w.WriteLine($"env->DeleteGlobalRef({CidsName}.{strct.Name});");

        foreach (ClassDecl clss in Module.AllClasses)
            w.WriteLine($"env->DeleteGlobalRef({CidsName}.{clss.Name});");

        w.CloseBrace();
    }

    private void GenerateCppJniFunctions(Writer w)
    {
        foreach (ClassDecl clss in Module.AllClasses)
        {
            string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
              CppCodeGenerator.NameContextFlags.ForceModulePrefix);

            foreach (FunctionDecl ctor in clss.Ctors)
            {
                w.ClearWriteMarker();
                _nameGen.Clear();

                var funcCaller = new JniToCppFuncCaller(w, _nameGen, ctor);

                w.Write($"JNIEXPORT jlong JNICALL {ctor.NativeNameInJNI}(JNIEnv* env, jclass clss_");
                if (ctor.Parameters.Any())
                    w.Write(", ");

                GenerateMethodParameters(w, ctor, NameContext.JniFunctionParameter);
                w.WriteLine(')');

                w.OpenBrace();
                funcCaller.GenerateCall(string.Empty);
                w.CloseBrace();

                if (ctor != clss.Ctors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // Class functions
            foreach (FunctionDecl func in clss.AllExportedFunctionsWithoutCtors)
            {
                _nameGen.Clear();

                w.Write(
                  $"JNIEXPORT {GetTypeName(func.ReturnType, NameContext.JniFunctionReturnType)} JNICALL {func.NativeNameInJNI}(JNIEnv* env, ");

                if (func.IsStatic)
                    w.Write("jclass clss_");
                else
                    w.Write("jobject " + Strings.JniThisParam);

                if (func.Parameters.Any())
                    w.Write(", ");

                foreach (FunctionParamDecl param in func.Parameters)
                {
                    w.Write(GetTypeName(param.Type, NameContext.JniFunctionParameter) + ' ' + param.Name);

                    if (param != func.Parameters[^1])
                        w.Write(", ");
                }

                w.Write(") ");
                w.OpenBrace();

                string implVarName = DeclareImplPointer(w, clss);
                string thisObjName = _nameGen.CreateNext();
                w.WriteLine($"{classNameInCpp} {thisObjName}{{{implVarName}}};");

                var funcCaller = new JniToCppFuncCaller(w, _nameGen, func);
                funcCaller.GenerateCall(thisObjName);

                w.CloseBrace();

                if (func != clss.AllExportedFunctionsWithoutCtors[^1])
                    w.WriteLine();
            }

            w.WriteNewlineIfWrittenAnythingAndResetMarker();

            // disposeNative()
            if (clss.IsDisposable)
            {
                _nameGen.Clear();

                w.WriteNewlineIfWrittenAnythingAndResetMarker();

                string disposeNativeFuncName = $"Java_{clss.QualifiedNameInJava}_disposeNative".Replace('.', '_');
                w.WriteLine($"JNIEXPORT void JNICALL {disposeNativeFuncName}(JNIEnv* env, jobject {Strings.JniThisParam})");

                w.OpenBrace();
                {
                    string implVarName = DeclareImplPointer(w, clss);

                    // Assign it to a reference-counted C++ object and do nothing with that object.
                    // This will ensure that the impl pointer is deleted.
                    string objName = _nameGen.CreateNext();
                    w.WriteLine($"{classNameInCpp} {objName}{{{implVarName}}};");
                }
                w.CloseBrace();
            }
        }
    }

    private void GenerateNativeExceptionJavaFile()
    {
        string filename = Path.Combine(Module.Paths.JavaOutputDirectory, "NativeException.java");

        var w = new Writer();

        w.WriteAutoGenerationNotice(filename,
          new[] { "Built-in exception class that is thrown when native function calls fail." },
          "//", false);

        GeneratePackageDeclaration(w);

        w.Write("public class NativeException extends RuntimeException ");
        w.OpenBrace();
        w.WriteLine("private static final long serialVersionUID = 1L;");
        w.WriteLine();
        w.Write("public NativeException(String message) ");
        w.OpenBrace();
        w.WriteLine("super(message);");
        w.CloseBrace();
        w.CloseBrace();

        w.SaveContentsToDisk(filename);
    }


    private void GenerateMethodParameters(Writer w, FunctionDecl function, NameContext context)
    {
        foreach (FunctionParamDecl param in function.Parameters)
        {
            w.Write(GetTypeName(param.Type, context) + ' ' + param.Name);

            if (param != function.Parameters[^1])
                w.Write(", ");
        }
    }


    private static void GenerateJavaMethodBody(Writer w, FunctionDecl function)
    {
        var funcCaller = new JavaToNativeFuncCaller(w, function);

        if (function.HasNonVoidReturnType)
            w.Write("return ");

        w.Write(function.NativeNameInJava + '(');
        funcCaller.GenerateArguments();
        w.Write(')');
        w.WriteLine(';');
    }


    private void GenerateJNIClassesExtraction(Writer w)
    {
        w.ClearWriteMarker();

        w.WriteLine("// Classes");
        w.OpenBrace();

        w.WriteLine("jclass handle;");
        w.WriteLine();

        void GenerateField(string fieldName, string javaTypeName, string javaTypeNameForFindClass)
        {
            w.WriteLine($"std::cout << \"Loading type {javaTypeName}\" << std::endl;");

            w.WriteLine($"handle = env->FindClass(\"{javaTypeNameForFindClass}\");");
            w.WriteLine($"{CidsName}.{fieldName} = (jclass)env->NewGlobalRef(handle);");
            w.WriteLine("env->DeleteLocalRef(handle);");
            w.WriteLine("if (env->ExceptionOccurred() || " + CidsName + '.' + fieldName + " == nullptr)");
            w.Indent();
            w.WriteLine("return JNI_ERR;");
            w.Unindent();
        }

        foreach ((string, string) pair in new[]
                 {
               ($"{Strings.ForbiddenIdentifierPrefix}JavaString", "Ljava/lang/String;")
             })
        {
            GenerateField(pair.Item1, pair.Item2, pair.Item2);
            w.WriteLine();
        }

        foreach (StructDecl strct in Module.AllStructs)
        {
            GenerateField(strct.NameInJava, strct.QualifiedNameInJava, strct.NameInJavaForFindClass);
            if (strct != Module.AllStructs[^1])
                w.WriteLine();
        }

        w.WriteNewlineIfWrittenAnythingAndResetMarker();

        foreach (ClassDecl clss in Module.AllClasses)
        {
            GenerateField(clss.NameInJava, clss.QualifiedNameInJava, clss.NameInJavaForFindClass);
            if (clss != Module.AllClasses[^1])
                w.WriteLine();
        }

        w.CloseBrace();
    }

    private void GenerateJNIMethodsExtraction(Writer w)
    {
        w.WriteLine("// Methods");
        w.OpenBrace();

        foreach (StructDecl strct in Module.AllStructs)
        {
            w.WriteLine(string.Format("{0}.{1}{2} = env->GetMethodID({3}.{1}, \"<init>\", \"()V\");", MidsName,
              strct.Name,
              CtorSuffix, CidsName));

            w.WriteLine($"if (env->ExceptionOccurred() || {MidsName}.{strct.Name}{CtorSuffix} == nullptr) return JNI_ERR;");
        }

        w.CloseBrace();
    }


    private void GenerateJNIFieldsExtraction(Writer w)
    {
        w.WriteLine("// Fields");
        w.OpenBrace();

        foreach (TypeDecl typeDecl in Module.AllTypeDecls(false))
            if (typeDecl is StructDecl strct)
            {
                foreach (StructFieldDecl field in strct.Fields)
                {
                    string fieldName = $"{FidsName}.{strct.Name}_{field.NameInJava}";
                    string typeIdInJava = GetJavaTypeSignature(field.Type);

                    w.WriteLine(
                      $"{fieldName} = env->GetFieldID({CidsName}.{strct.Name}, \"{field.NameInJava}\", \"{typeIdInJava}\");");

                    w.WriteLine($"if (env->ExceptionOccurred() || {fieldName} == nullptr)");
                    w.Indent();
                    w.WriteLine("return JNI_ERR;");
                    w.Unindent();
                }
            }
            else if (typeDecl is ClassDecl clss)
            {
                // ptr field of the class
                w.Write($"{FidsName}.{clss.Name}_ptr");
                w.WriteLine($" = env->GetFieldID({CidsName}.{clss.Name}, \"ptr\", \"J\");");
                w.WriteLine("if (env->ExceptionOccurred() || " + FidsName + '.' + clss.Name + "_ptr == nullptr)");
                w.Indent();
                w.WriteLine("return JNI_ERR;");
                w.Unindent();
            }
            else if (typeDecl is DelegateDecl)
            {
            }

        w.CloseBrace();
    }


    private static void GenerateStructToStringOverride(Writer w, StructDecl strct)
    {
        if (!strct.Fields.Any())
            return;

        w.WriteLine("@Override");
        w.Write("public String toString() ");
        w.OpenBrace();

        if (strct.Fields.Count > 1u)
        {
            w.WriteLine("StringBuilder sb = new StringBuilder(32);");

            const int maxFields = 4;
            int howManyFields = Math.Min(strct.Fields.Count(), maxFields);

            for (int i = 0; i < howManyFields; ++i)
            {
                StructFieldDecl field = strct.Fields.ElementAt(i);

                w.WriteLine($"sb.append(\"{field.NameInJava}=\");");
                w.WriteLine($"sb.append({field.NameInJava});");

                if (i + 1 < howManyFields)
                    w.WriteLine("sb.append(\"; \");");
            }

            w.WriteLine("return sb.toString();");
        }
        else
        {
            Debug.Assert(strct.Fields.Count == 1u);
            w.WriteLine($"return {strct.Fields[0].NameInJava}.toString();");
        }

        w.CloseBrace();
    }

    private string DeclareImplPointer(Writer w, ClassDecl clss)
    {
        string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);
        string implVarName = _nameGen.CreateNext();

        // Get the impl pointer from the jobject.
        w.WriteLine(
          $"const auto {implVarName} = reinterpret_cast<{classNameInCpp}Impl*>(env->GetLongField({Strings.JniThisParam}, {FidsName}.{clss.Name}_ptr));");

        return implVarName;
    }

    public static string GetTypeName(IDataType type, NameContext context)
    {
        var arrayType = type as ArrayType;

        if (arrayType != null)
            type = arrayType.ElementType;

        string typeName = type.Name;

        if (!type.IsUserDefined)
        {
            if (type == PrimitiveType.String)
            {
                switch (context)
                {
                    case NameContext.MethodParameter or NameContext.MethodReturnType or NameContext.NativeMethodParameter
                    or NameContext.NativeMethodReturnType:
                        typeName = "String";
                        break;
                    case NameContext.JniFunctionParameter:
                    case NameContext.JniFunctionReturnType:
                        typeName = "jstring";
                        break;
                    default:
                        throw new InvalidOperationException("invalid context " + context);
                }
            }
            else
            {
                if (!_primTypeNameMap.TryGetValue(type, out (string, string) it))
                    throw new CompileError($"invalid inner type \"{type.Name}\"", null, ErrorCategory.Internal);

                typeName = context is NameContext.JniFunctionParameter or NameContext.JniFunctionReturnType
                  ? it.Item1
                  : it.Item2;
            }
        }
        else
        {
            if (type.IsClass && context is NameContext.JniFunctionReturnType)
                typeName = "jlong";
            else if (context is NameContext.JniFunctionParameter or NameContext.JniFunctionReturnType)
                typeName = "jobject";
        }

        if (arrayType != null)
        {
            if (context is NameContext.JniFunctionParameter or NameContext.JniFunctionReturnType)
            {
                if (typeName == "jstring")
                    typeName = "jobjectArray";
                else
                    typeName += "Array";
            }
            else
            {
                typeName += "[]";
            }
        }

        return typeName;
    }

    private static string GetJavaTypeSignature(IDataType type)
    {
        if (type.IsUserDefined)
            return $"L{type.Name};";

        if (type == PrimitiveType.String)
            return "Ljava/lang/String;";
        if (type == PrimitiveType.Bool)
            return "Z";
        if (type == PrimitiveType.Byte)
            return "B";
        if (type == PrimitiveType.Short)
            return "S";
        if (type == PrimitiveType.Int)
            return "I";
        if (type == PrimitiveType.Long || type == PrimitiveType.Handle)
            return "J";
        if (type == PrimitiveType.Float)
            return "F";
        if (type == PrimitiveType.Double)
            return "D";
        if (type == PrimitiveType.Void)
            return "V";
        throw new CompileError("invalid inner type", null, ErrorCategory.Internal);
    }

    public enum NameContext
    {
        None = 1,
        MethodReturnType,
        MethodParameter,
        NativeMethodReturnType,
        NativeMethodParameter,
        JniFunctionReturnType,
        JniFunctionParameter
    }
}