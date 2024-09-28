using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

public sealed class PythonCodeGenerator(Module module) : CodeGenerator(module)
{
    private const string g_ModVarName = "m_";
    private readonly TempVarNameGen _nameGen = new("brigen_");

    public override void Generate()
    {
        var modl = Module;
        var paths = modl.Paths;

        Logger.LogCategoryGenerationStatus("Python");
        Logger.LogFileGenerationStatus("Combined Code", paths.PythonCppFile);

        Directory.CreateDirectory(Path.GetDirectoryName(paths.PythonCppFile)!);

        var w = new Writer();

        w.WriteAutoGenerationNotice(paths.PythonCppFile, [$"This is the Python wrapper file for {Module.Name}."], "//", false);

        w.WriteLine("#include <pybind11/functional.h>");
        w.WriteLine("#include <pybind11/pybind11.h>");
        w.WriteLine("#include <pybind11/pytypes.h>");
        w.WriteLine("#include <pybind11/stl.h>");
        w.WriteLine();
        w.WriteLine($"#define {modl.Name}_EXPORTS 1");
        w.WriteLine($"#include <{modl.Name}.hpp>");
        w.WriteLine();
        w.WriteLine("#include <string>");
        w.WriteLine("#include <vector>");
        w.WriteLine();
        w.WriteLine("namespace py = pybind11;");
        w.WriteLine();

        w.WriteLine($"PYBIND11_MODULE({modl.PythonLibName}, {g_ModVarName})");
        w.OpenBrace();

        w.WriteLine($"{g_ModVarName}.doc() = R\"pbdoc(");
        w.Indent();
        w.WriteLine(modl.Description);
        w.Unindent();
        w.WriteLine(")pbdoc\";");
        w.WriteLine();

        // Enums
        foreach (var enm in modl.AllEnums)
        {
            GenerateEnum(w, enm);

            if (enm != modl.AllEnums[^1])
                w.WriteLine();
        }

        if (modl.AllEnums.Any() && modl.AllStructs.Any())
            w.WriteLine();

        // structs
        foreach (var strct in modl.AllStructs)
        {
            GenerateStruct(w, strct);

            if (strct != modl.AllStructs[^1])
                w.WriteLine();
        }

        if (modl.AllClasses.Any())
            w.WriteLine();

        // Interfaces
        foreach (var clss in modl.AllClasses)
        {
            GenerateClass(w, clss);

            if (clss != modl.AllClasses[^1])
                w.WriteLine();
        }

        w.WriteLine();
        w.WriteLine($@"{g_ModVarName}.attr(""__version__"") = ""dev"";");

        w.CloseBrace();

        w.SaveContentsToDisk(paths.PythonCppFile);
    }

    void GenerateEnum(Writer w, EnumDecl enm)
    {
        string enumNameInCpp = CppCodeGenerator.GetTypeName(Module, enm, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.WriteLine($"py::enum_<{enumNameInCpp}>({g_ModVarName}, \"{enm.Name}\")");
        w.Indent();

        foreach (var member in enm.Members)
        {
            w.WriteLine($".value(\"{member.Name}\", {enumNameInCpp}::{member.Name})");
        }

        w.WriteLine(';');
        w.Unindent();
    }

    void GenerateStruct(Writer w, StructDecl strct)
    {
        var modl = Module;

        string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.WriteLine($"py::class_<{structNameInCpp}>({g_ModVarName}, \"{strct.Name}\")");
        w.Indent();

        // Default ctor of the struct
        w.WriteLine(".def(py::init())");

        // Overloaded ctor
        if (strct.Fields.Any())
        {
            w.Write(".def(py::init<");

            foreach (var field in strct.Fields)
            {
                w.Write(CppCodeGenerator.GetTypeName(modl, field.Type, CppCodeGenerator.NameContext.StructField,
                  CppCodeGenerator.NameContextFlags.ForceModulePrefix));

                if (field != strct.Fields[^1])
                    w.Write(", ");
            }

            w.Write(">(), ");

            foreach (var field in strct.Fields)
            {
                w.Write($"py::arg(\"{field.Name.CamelCased()}\")");

                if (field != strct.Fields[^1])
                    w.Write(", ");
            }

            w.Write(')');
            w.WriteLine();
        }

        // Fields
        foreach (var field in strct.Fields)
            w.WriteLine($".def_readwrite(\"{field.Name}\", &{structNameInCpp}::{field.Name})");

        GenerateStructRepr(w, strct);
        w.WriteLine();
        GenerateStructHashMethod(w, strct);
        w.WriteLine();
        GenerateStructBasicOperators(w, strct);

        w.WriteLine(';');
        w.Unindent();
    }

    void GenerateStructRepr(Writer w, StructDecl strct)
    {
        string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.WriteLine($".def(\"__repr__\", [](const {structNameInCpp}& o) {{");
        w.Indent();

        w.WriteLine("std::string ret;");
        w.WriteLine("ret += '<';");

        foreach (var field in strct.Fields)
        {
            w.WriteLine($"ret += \"{field.Name}:\";");
            w.Write("ret += ");

            if (field.Type.IsUserDefined)
            {
                // Print UDTs just as "{UDTName}" to keep the string short.
                w.Write('\"');
                w.Write(field.Type.Name);
                w.Write('\"');
            }
            else if (field.Type == PrimitiveType.Bool)
            {
                w.Write($"o.{field.Name} ? \"true\" : \"false\"");
            }
            else if (field.Type == PrimitiveType.String)
            {
                w.Write($"o.{field.Name}");
            }
            else
            {
                // Must use std::to_string().
                if (field.Type == PrimitiveType.Handle)
                {
                    w.Write($"std::to_string(reinterpret_cast<intptr_t>(o.{field.Name}))");
                }
                else
                {
                    w.Write($"std::to_string(o.{field.Name})");
                }
            }

            w.WriteLine(';');

            if (field != strct.Fields[^1])
                w.WriteLine("ret += \"; \";");
        }

        w.WriteLine("ret += '>';");
        w.WriteLine("return ret;");

        w.Unindent();
        w.Write("})");
    }

    void GenerateStructHashMethod(Writer w, StructDecl strct)
    {
        string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.WriteLine($".def(\"__hash__\", [](const {structNameInCpp}& o) {{");
        w.Indent();
        w.WriteLine("return GetHashCode(o);");
        w.Unindent();
        w.Write("})");
    }

    void GenerateStructBasicOperators(Writer w, StructDecl strct)
    {
        string structNameInCpp = CppCodeGenerator.GetTypeName(Module, strct, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.WriteLine(string.Format(".def(\"__eq__\", [](const {0}&lhs, const {0}& rhs) {{", structNameInCpp));
        w.Indent();
        w.WriteLine("return lhs == rhs;");
        w.Unindent();
        w.Write("})");
    }

    void GenerateClass(Writer w, ClassDecl clss)
    {
        string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.OpenBrace();

        // Declaration & initialization
        w.WriteLine($"auto clss_ = py::class_<{classNameInCpp}>({g_ModVarName}, \"{clss.Name}\");");

        // Ctors
        foreach (var ctor in clss.Ctors)
            GenerateClassCtor(w, ctor);

        // Functions
        foreach (var func in clss.Functions)
            GenerateClassFunction(w, func);

        // Properties
        foreach (var prop in clss.Properties)
            GenerateProperty(w, prop);

        if (clss.Comment != null)
        {
            w.WriteLine("clss_.doc() = R\"pbdoc(");
            w.Indent();

            foreach (var line in clss.Comment.ContentLines)
                w.WriteLine(line);

            w.Unindent();
            w.WriteLine(")pbdoc\";");
        }

        w.CloseBrace();
    }

    bool NeedLambdaExpressionForMethod(FunctionDecl func)
    {
        if (func.HasNonVoidReturnType && func.ReturnType.IsArray)
            return true;

        foreach (var param in func.Parameters)
        {
            if (param.Type.IsDelegate)
                return true;

            if (param.Type.IsArray)
                return true;
        }

        return false;
    }

    void GenerateClassCtor(Writer w, FunctionDecl func)
    {
        Debug.Assert(func.IsCtor);

        var clss = func.ParentAsClass;
        Debug.Assert(clss != null);

        string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        w.Write($"clss_.def(py::init(&{classNameInCpp}::{func.Name})");

        if (func.Parameters.Any())
            w.Write(", ");

        foreach (var param in func.Parameters)
        {
            w.Write($"py::arg(\"{param.Name}\")");

            if (param != func.Parameters[^1])
                w.Write(", ");
        }

        w.WriteLine(");");
    }

    void GenerateClassFunction(Writer w, FunctionDecl func)
    {
        // TODO: This method has too much noise. Break it up into smaller pieces.
        // The indentation is criminal.

        var modl = Module;

        Debug.Assert(!func.IsCtor);

        var clss = func.ParentAsClass;
        Debug.Assert(clss != null);

        string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        if (NeedLambdaExpressionForMethod(func))
        {
            w.Write($"clss_.def(\"{func.Name}\", [](");

            if (func.IsStatic)
                throw new NotImplementedException();

            if (func.IsConst)
                w.Write("const ");

            w.Write($"{classNameInCpp}& {Strings.CApiThisParam}");

            if (func.Parameters.Any())
                w.Write(", ");

            // Lambda signature
            foreach (var param in func.Parameters)
            {
                var paramType = param.Type;
                if (paramType.IsDelegate)
                {
                    w.Write($"py::function& {param.Name}");
                }
                else if (paramType.IsArray)
                {
                    string arrTypeName = string.Empty;

                    if (paramType == PrimitiveType.Byte)
                    {
                        arrTypeName = "const py::bytes&";
                    }
                    else
                    {
                        arrTypeName += "const std::vector<";
                        if (paramType == PrimitiveType.String)
                        {
                            arrTypeName += "std::string";
                        }
                        else
                        {
                            var arrType = paramType as ArrayType;
                            Debug.Assert(arrType != null);

                            arrTypeName += CppCodeGenerator.GetTypeName(modl, arrType.ElementType,
                              CppCodeGenerator.NameContext.FunctionParam,
                              CppCodeGenerator.NameContextFlags.ForceModulePrefix |
                              CppCodeGenerator.NameContextFlags.ForcePassByValue);
                        }

                        arrTypeName += ">&";
                    }

                    w.Write($"{arrTypeName} {param.Name}");
                }
                else
                {
                    w.Write(CppCodeGenerator.GetTypeName(modl, paramType, CppCodeGenerator.NameContext.FunctionParam,
                      CppCodeGenerator.NameContextFlags.ForceModulePrefix));
                    w.Write($" {param.Name}");
                }

                if (param != func.Parameters[^1])
                    w.Write(", ");
            }

            w.WriteLine(")");
            w.OpenBrace();

            // Declare necessary variables.
            foreach (var param in func.Parameters)
            {
                var paramType = param.Type;
                if (paramType.IsArray)
                {
                    if (paramType == PrimitiveType.Byte)
                    {
                        var bufferVarName = param.Name + "_buffer";
                        var bufferSizeVarName = param.Name + "_bufferSize";

                        var arrayViewTypeName =
                          CppCodeGenerator.GetTypeName(modl, paramType, CppCodeGenerator.NameContext.FunctionParam,
                            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

                        w.WriteLine($"char* {bufferVarName} = nullptr;");
                        w.WriteLine($"ssize_t {bufferSizeVarName} = ssize_t(0);");
                        w.WriteLine($"PyBytes_AsStringAndSize({param.Name}.ptr(), &{bufferVarName}, &{bufferSizeVarName});");

                        w.WriteLine(
                          $"var {param.Name}_view = {arrayViewTypeName}(reinterpret_cast<const uint8_t*>({bufferVarName}), static_cast<size_t>({bufferSizeVarName}));");

                        w.WriteLine("^^^ NOT IMPLEMENTED ^^^");
                    }
                    else
                    {
                        var viewParamRef = param.Name;

                        // vector -> ArrayView
                        if (paramType == PrimitiveType.String)
                        {
                            const string strptrsSuffix = "_strptrs";

                            viewParamRef += strptrsSuffix;

                            // Have to create an extra std::vector<const char*> so we can
                            // create a ArrayView<const char*>, which is expected by the C/C++
                            // API.

                            // Size declaration
                            var sizeName = $"{param.Name}_size";
                            w.WriteLine($"const auto {sizeName} = static_cast<uint32_t>({param.Name}.size());");

                            // std::vector<const char*> declaration
                            w.WriteLine($"vector<const char*> {param.Name}{strptrsSuffix}{{{sizeName}}};");

                            // Copy over the c_strs.
                            var counterName = viewParamRef + "_i";
                            w.Write(string.Format("foreach (size_t {0} = 0u; {0} < {1}; ++{0})", counterName, sizeName));
                            w.OpenBrace();
                            w.Write($"{viewParamRef}[{counterName}] = {param.Name}[{counterName}].c_str()");
                            w.WriteLine(";");
                            w.CloseBrace();
                        }

                        var arrType = paramType as ArrayType;
                        Debug.Assert(arrType != null);

                        w.WriteLine($"const auto {param.Name}_arrptr = {viewParamRef}.data();");
                        w.WriteLine($"const auto {param.Name}_arrsize = static_cast<uint32_t>({viewParamRef}.size());");
                    }
                }
            }

            // Call the C++ method.
            {
                if (func.HasNonVoidReturnType)
                    w.Write("const auto ret = ");

                w.Write($"{Strings.CApiThisParam}.{func.Name}(");

                foreach (var param in func.Parameters)
                {
                    var type = param.Type;

                    if (type.IsArray)
                    {
                        w.Write($"{param.Name}_arrptr, {param.Name}_arrsize");
                    }
                    else if (type is DelegateDecl delg)
                    {
                        var nameGen = new TempVarNameGen(Strings.ForbiddenIdentifierPrefix);

                        w.Write($"[](void* {nameGen.CreateNext()}");
                        if (delg.Parameters.Any())
                            w.Write(", ");

                        foreach (var delgParam in delg.Parameters)
                        {
                            // String arrays need special handling.
                            if (delgParam.Type.IsArray && delgParam.Type == PrimitiveType.String)
                            {
                                w.Write("const char* const*");
                            }
                            else
                            {
                                w.Write(CppCodeGenerator.GetTypeName(
                                  modl, delgParam.Type, CppCodeGenerator.NameContext.FunctionParam,
                                  CppCodeGenerator.NameContextFlags.ForceModulePrefix));
                            }

                            w.Write(' ');

                            var delgParamName = nameGen.CreateNext();
                            w.Write(delgParamName);

                            if (delgParam.Type.IsArray)
                                w.Write($", uint32_t {delgParamName}sz");

                            if (delgParam != delg.Parameters[^1])
                                w.Write(", ");
                        }

                        w.WriteLine(")");
                        w.WriteLine("{");
                        w.Indent();

                        // Declare any necessary variables.
                        foreach (var innerParam in func.Parameters)
                        {
                            if (!innerParam.Type.IsDelegate)
                                continue;

                            int j = 1;
                            foreach (var delgParam in delg.Parameters)
                            {
                                if (delgParam.Type.IsArray)
                                {
                                    var parName = nameGen[j];
                                    var szName = $"{parName}sz";
                                    w.WriteLine($"vector<string> {parName}vec{{static_cast<size_t>({szName})}};");

                                    w.Write(string.Format("for (uint32_t {0} = 0u; {0} < {1}; ++{0}) ", $"{parName}sz_i",
                                      $"{parName}sz"));
                                    w.Write(string.Format("{0}[{1}] = {2}[{1}];", $"{parName}vec", $"{parName}sz_i", parName));
                                    w.WriteLine();
                                }

                                ++j;
                            }
                        }

                        // Return if the delegate has a return-value.
                        bool returnsSomething = delg.ReturnType != PrimitiveType.Void;

                        var returnValueName = string.Empty;

                        if (returnsSomething)
                        {
                            returnValueName = nameGen.CreateNext();
                            w.Write($"const auto {returnValueName} = ");
                        }

                        w.Write($"(*static_cast<py::function*>({Strings.ForbiddenIdentifierPrefix}0))(");

                        // Arguments
                        int i = 1;
                        foreach (var delgParam in delg.Parameters)
                        {
                            if (delgParam.Type.IsArray)
                            {
                                w.Write($"{nameGen[i]}vec");
                            }
                            else
                            {
                                w.Write(nameGen[i]);
                            }

                            if (delgParam != delg.Parameters[^1])
                                w.Write(", ");

                            ++i;
                        }

                        w.Write(");");

                        // End of functor call, now let's see what we can do with the result.
                        if (delg.ReturnType.IsUserDefined)
                        {
                            var returnTypeName = CppCodeGenerator.GetTypeName(modl, delg.ReturnType,
                              CppCodeGenerator.NameContext.DelegateReturnType,
                              CppCodeGenerator.NameContextFlags.ForceReturnByValue |
                              CppCodeGenerator.NameContextFlags.ForceModulePrefix);

                            w.Write($"return py::cast<{returnTypeName}>({returnValueName})");
                        }
                        else if (delg.ReturnType == PrimitiveType.String)
                        {
                            w.WriteLine($"return PyUnicode_AsUTF8({returnValueName}.ptr());");
                        }

                        w.Unindent();
                        w.Write('}');
                    }
                    else
                    {
                        w.Write(param.Name);
                    }

                    if (param != func.Parameters[^1])
                        w.Write(", ");
                }

                // Pass the SysValues for delegate parameters.
                {
                    var delgParams = func.DelegateParameters.ToArray();
                    if (delgParams.Any())
                        w.Write(", ");

                    foreach (var delgParam in delgParams)
                    {
                        w.Write($"std::addressof({delgParam.Name})");
                        if (delgParam != delgParams[^1])
                            w.Write(", ");
                    }
                }

                w.WriteLine(");");
            }

            if (func.HasNonVoidReturnType)
            {
                var returnType = func.ReturnType;
                if (returnType.IsArray)
                {
                    if (returnType.IsUserDefined)
                    {
                        w.WriteLine("return ret;");
                    }
                    else if (returnType == PrimitiveType.Byte)
                    {
                        w.WriteLine(
                          "return py::bytes(reinterpret_cast<const char*>(ret.data()), static_cast<py::size_t>(ret.size()));");
                    }
                    else
                    {
                        w.WriteLine("return ret;");
                    }
                }
                else
                {
                    w.WriteLine("return ret;");
                }
            }

            w.Unindent();
            w.WriteLine("});");
        }
        else
        {
            // Normal method generation
            w.Write("clss_.");
            w.Write(func.IsStatic ? "def_static" : "def");
            w.Write(string.Format("(\"{0}\", &{1}::{0}", func.Name, classNameInCpp));

            foreach (var param in func.Parameters)
            {
                w.Write($", py::arg(\"{param.Name}\")");
            }

            if (func.Comment != null)
            {
                var comment = func.Comment;

                w.WriteLine(", R\"(");
                w.IndentationEnabled = false;
                foreach (var line in comment.ContentLines)
                    w.WriteLine(line);

                if (func.Parameters.Any() && comment.ParameterDescriptions.Any())
                {
                    w.WriteLine();
                    w.WriteLine("Parameters:");
                    foreach (var (item1, item2) in comment.ParameterDescriptions)
                        w.WriteLine($"  {item1}: {item2}");
                }

                w.Write(")\"");
                w.IndentationEnabled = true;
            }

            w.WriteLine(");");
        }
    }


    void GenerateProperty(Writer w, PropertyDecl prop)
    {
        var clss = prop.ParentAsClass;
        Debug.Assert(clss != null);

        string classNameInCpp = CppCodeGenerator.GetTypeName(Module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        var getterFunc = clss.GetFunctionForProperty(prop, PropertyDecl.PropMask.Getter);
        var setterFunc = clss.GetFunctionForProperty(prop, PropertyDecl.PropMask.Setter);

        if (prop.HasGetter && prop.HasSetter)
        {
            Debug.Assert(getterFunc != null);
            Debug.Assert(setterFunc != null);
            w.Write(
              $"clss_.def_property(\"{prop.Name}\", &{classNameInCpp}::{getterFunc.Name}, &{classNameInCpp}::{setterFunc.Name})");
        }
        else if (prop.HasGetter && !prop.HasSetter)
        {
            Debug.Assert(getterFunc != null);
            w.Write($"clss_.def_property_readonly(\"{prop.Name}\", &{classNameInCpp}::{getterFunc.Name})");
        }
        else
        {
            Debug.Assert(setterFunc != null);

            // No getter, but a setter. We don't emit this as a property, because pybind11
            // doesn't have something like "def_property_writeonly". Therefore we emit this
            // property as a normal method.
            w.Write($"clss_.def(\"{setterFunc.Name}\", &{classNameInCpp}::{setterFunc.Name})");
        }

        w.WriteLine(';');
    }
}