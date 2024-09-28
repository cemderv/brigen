using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

/// <summary>
/// Handles calls from C to the library's C++ API.
/// </summary>
internal sealed class CToCppFuncCaller : IDisposable
{
    private readonly List<(FunctionParamDecl, string)> _classVars;
    private readonly Module _module;
    private readonly FunctionDecl _function;
    private readonly TempVarNameGen _nameGen;
    private readonly Writer _writer;
    private string _thisVarName = string.Empty;

    public CToCppFuncCaller(Writer writer, TempVarNameGen nameGen, FunctionDecl function)
    {
        Debug.Assert(function.Module != null);

        _writer = writer;
        _nameGen = nameGen;
        _module = function.Module!;
        _function = function;
        _classVars = [];
    }

    public void Dispose()
    {
    }

    public void GenerateCall()
    {
        if (_function.IsCtor)
            GenerateConstructorCall();
        else
            GenerateNormalFunctionCall();
    }

    private void GenerateConstructorCall()
    {
        string typeNameC =
          CCodeGenerator.GetTypeName(_function.ParentTypeDecl!, CCodeGenerator.NameContext.Neutral);

        string typeNameCpp =
          CppCodeGenerator.GetTypeName(_module, _function.ParentTypeDecl!, CppCodeGenerator.NameContext.Neutral,
            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        DeclareVars();

        string returnValueVarName = _nameGen.CreateNext();

        _writer.Write($"auto {returnValueVarName} = {typeNameCpp}::{_function.Name}(");
        GenerateArguments();
        _writer.WriteLine(");");

        _writer.WriteLine($"if (!{returnValueVarName}) {{ return nullptr; }}");

        string implVarName = _nameGen.CreateNext();
        _writer.WriteLine(
          $"auto {implVarName} = {returnValueVarName}.{NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.GetImpl, _module)}();");
        _writer.WriteLine(
          $"{returnValueVarName}.{NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.DropImpl, _module)}();");
        Closure();

        _writer.WriteLine($"return reinterpret_cast<{typeNameC}*>({implVarName});");
    }

    private void GenerateNormalFunctionCall()
    {
        if (_function.ReturnType.IsClass)
            // Function returns an class (via a pointer).
            GenerateFunctionCall_ReturnsClass();
        else if (_function.HasNonVoidReturnType && !_function.HasOutReturnParam)
            // Function returns a normal value (by-value).
            GenerateFunctionCall_ReturnsByValue();
        else if (_function.HasNonVoidReturnType && _function.HasOutReturnParam)
            // Function returns its value via an out-parameter.
            GenerateFunctionCall_ReturnsViaOutParameter();
        else if (!_function.HasNonVoidReturnType)
            // Function does not have any return value.
            GenerateFunctionCall_ReturnsNothing();
        else
            throw new InvalidOperationException("invalid function call");
    }

    private void DeclareVars()
    {
        _classVars.Clear();

        if (_function.HasThisParam)
        {
            ClassDecl? clss = _function.ParentAsClass;
            Debug.Assert(clss != null);

            string classNameInCpp = CppCodeGenerator.GetTypeName(_module, _function.ParentTypeDecl!,
              CppCodeGenerator.NameContext.Neutral, CppCodeGenerator.NameContextFlags.ForceModulePrefix);

            bool isConstThis = _function.IsConst;

            _thisVarName = _nameGen.CreateNext();

            if (isConstThis)
                _writer.Write("const ");

            _writer.Write($"auto {_thisVarName} = {classNameInCpp}(");

            if (isConstThis)
                // Cast away const
                _writer.Write($"const_cast<{clss.QualifiedImplClassName}*>(");

            // Reinterpret to impl pointer
            _writer.Write("reinterpret_cast<");
            if (isConstThis)
                _writer.Write("const ");
            _writer.Write($"{clss.QualifiedImplClassName}*>(");

            _writer.Write(Strings.CApiThisParam);

            _writer.Write(')'); // Close reinterpret_cast

            if (isConstThis)
                _writer.Write(')'); // Close const_cast

            _writer.Write(')'); // Close class constructor call

            _writer.WriteLine(';');

            _classVars.Add((_module.ThisParam, _thisVarName));
        }

        foreach (FunctionParamDecl parameter in _function.Parameters)
        {
            IDataType type = parameter.Type;

            if (parameter.Type.IsClass && !type.IsArray)
            {
                var clss = type as ClassDecl;
                Debug.Assert(clss != null);

                string classNameInCpp = CppCodeGenerator.GetTypeName(_module, clss, CppCodeGenerator.NameContext.Neutral,
                  CppCodeGenerator.NameContextFlags.ForceModulePrefix);
                string paramNameObj = _nameGen.CreateNext();

                _writer.WriteLine(
                  $"auto {paramNameObj} = {classNameInCpp}(reinterpret_cast<{clss.QualifiedImplClassName}*>({parameter.Name}));");

                _classVars.Add((parameter, paramNameObj));
            }
        }
    }

    private void GenerateArguments()
    {
        foreach (FunctionParamDecl param in _function.Parameters)
        {
            IDataType paramType = param.Type;

            if (paramType is ClassDecl clss)
            {
                GenerateClassArgument(clss, param);
            }
            else if (paramType is StructDecl strct)
            {
                GenerateStructArgument(strct, param);
            }
            else if (paramType is DelegateDecl delg)
            {
                GenerateDelegateArgument(delg, param);
            }
            else if (paramType is EnumDecl enm)
            {
                string enumNameInCpp = CppCodeGenerator.GetTypeName(_module, enm, CppCodeGenerator.NameContext.Neutral,
                  CppCodeGenerator.NameContextFlags.ForceModulePrefix);
                _writer.Write($"{enumNameInCpp}({param.Name})");
            }
            else
            {
                string castExpr = string.Empty;
                {
                    if (paramType is ArrayType arrayType)
                    {
                        if (arrayType.ElementType is TypeDecl typeDecl)
                        {
                            string typeDeclNameInCpp =
                              CppCodeGenerator.GetTypeName(_module, typeDecl, CppCodeGenerator.NameContext.Neutral,
                                CppCodeGenerator.NameContextFlags.ForceModulePrefix);

                            castExpr = $"reinterpret_cast<const {typeDeclNameInCpp}*>(";
                        }
                        else if (arrayType.ElementType == PrimitiveType.Bool)
                        {
                            castExpr = $"reinterpret_cast<const bool32_t*>(";
                        }
                    }
                    else if (paramType == PrimitiveType.Bool)
                    {
                        castExpr = $"static_cast<bool32_t>(";
                    }
                }

                if (!string.IsNullOrEmpty(castExpr))
                    _writer.Write(castExpr);

                _writer.Write(param.Name);

                if (!string.IsNullOrEmpty(castExpr))
                    _writer.Write(")");
            }

            if (param.Type.IsArray)
                _writer.Write($", {param.Name}Size");

            if (param != _function.Parameters[^1])
                _writer.Write(", ");
        }

        if (_function.HasOutReturnParam && _function.ReturnType is ArrayType outArrayType)
        {
            if (_function.Parameters.Any())
                _writer.Write(", ");

            string typeNameToCastTo = string.Empty;
            {
                if (outArrayType.ElementType is TypeDecl typeDecl)
                {
                    typeNameToCastTo = CppCodeGenerator.GetTypeName(_module, typeDecl, CppCodeGenerator.NameContext.Neutral,
                      CppCodeGenerator.NameContextFlags.ForceModulePrefix);
                }
                // MyLib_Bools can and have to be reinterpreted to MyLib::Bools, as they have the
                // same memory layout, but are considered different types.
                else if (outArrayType.ElementType == PrimitiveType.Bool)
                {
                    typeNameToCastTo = "bool32_t";
                }
            }

            if (!string.IsNullOrEmpty(typeNameToCastTo))
                _writer.Write($"reinterpret_cast<{typeNameToCastTo}*>(");

            _writer.Write("resultArray");

            if (!string.IsNullOrEmpty(typeNameToCastTo))
                _writer.Write(")");

            _writer.Write(", resultArraySize");
        }

        // Pass the SysValues for delegate parameters.
        {
            var delgParams = _function.DelegateParameters;
            var functionParamDecls = delgParams as FunctionParamDecl[] ?? delgParams.ToArray();

            if (functionParamDecls.Length != 0 && _function.Parameters.Any())
                _writer.Write(", ");

            foreach (FunctionParamDecl delgParam in functionParamDecls)
            {
                var delgType = delgParam.Type as DelegateDecl;
                Debug.Assert(delgType != null);

                string delgTypeRefStr = CppCodeGenerator.GetTypeName(_module, delgType, CppCodeGenerator.NameContext.Neutral,
                  CppCodeGenerator.NameContextFlags.ForceModulePrefix);

                _writer.Write($"reinterpret_cast<{delgTypeRefStr}*>({delgParam.Name})");

                if (delgParam != functionParamDecls[^1])
                    _writer.Write(", ");
            }
        }
    }

    private void Closure()
    {
    }

    private void GenerateStructArgument(StructDecl _, FunctionParamDecl param)
    {
        IDataType type = param.Type;

        bool shouldDeref = !param.Type.IsArray;

        if (shouldDeref)
            _writer.Write("*");

        var strct = type as StructDecl;
        Debug.Assert(strct != null);

        string structNameInCpp = CppCodeGenerator.GetTypeName(_module, strct, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        _writer.Write($"reinterpret_cast<const {structNameInCpp}*>({param.Name})");
    }

    private void GenerateClassArgument(ClassDecl clss, FunctionParamDecl param)
    {
        IDataType paramType = param.Type;

        if (!paramType.IsArray)
        {
            (FunctionParamDecl, string) it = _classVars.Find(d => d.Item1 == param);
            _writer.Write(it.Item2);
        }
        else
        {
            // Pass an array of classes by reinterpreting the pointer.
            const string smallSize = "8";

            string classNameInC = CCodeGenerator.GetTypeName(clss, CCodeGenerator.NameContext.Neutral);
            string classNameInCpp = CppCodeGenerator.GetTypeName(_module, clss, CppCodeGenerator.NameContext.Neutral,
              CppCodeGenerator.NameContextFlags.ForceModulePrefix);

            _writer.Write(
              $"{Strings.CppInternalNamespace}::ClassBuffer<{classNameInCpp}, {classNameInC}, {smallSize}>(");
            _writer.Write($"{param.Name}, {param.Name}Size");
            _writer.Write(")");
            _writer.Write(".GetData()");
        }
    }

    private void GenerateDelegateArgument(DelegateDecl delg, FunctionParamDecl param)
    {
        // Redirect the C++ function pointer to a local lambda, in which we then
        // call the C respective function pointer.
        _writer.Write("[](");

        string sysValueVarName = _nameGen.CreateNext();

        var delgParamNames = new List<string>();
        CppCodeGenerator.GenerateDelegateParameters(_writer, delg, sysValueVarName, true, delgParamNames,
          _nameGen, true);

        _writer.Write(") ");
        _writer.OpenBrace();

        // Return, if the delegate has a return-value.
        bool returnsSomething = delg.ReturnType != PrimitiveType.Void;

        bool isReturnValueCast = false;

        if (returnsSomething)
        {
            if (delg.ReturnType is TypeDecl)
                // The delegate returns a UDT, so we have to cast to be sure.
                isReturnValueCast = true;

            _writer.Write("return ");

            if (isReturnValueCast)
            {
                string returnTypeName =
                  CppCodeGenerator.GetTypeName(delg.Module!, delg.ReturnType, CppCodeGenerator.NameContext.DelegateReturnType,
                    CppCodeGenerator.NameContextFlags.ForceReturnByValue | CppCodeGenerator.NameContextFlags.ForceModulePrefix);

                _writer.Write($"reinterpret_cast<const {returnTypeName}*>(");
            }
        }

        // Allocate any values we might need to pass to the C function pointer.
        var classVarMap = new Dictionary<FunctionParamDecl, string>();

        int i = 0;
        foreach (FunctionParamDecl delgParam in delg.Parameters)
        {
            if (delgParam.Type.IsClass)
            {
                // C++ class objects cannot be passed just like that. We have to assign
                // them to their C objects, which are passed to the function instead.
                if (delgParam.Type.IsArray)
                {
                    // Pass an array of classes.
                    throw new NotImplementedException("array of classes");
                }

                // Pass a single lass.
                string implVarName = _nameGen.CreateNext();
                _writer.Write($"auto {implVarName} = ");
#if false
        // arg1: delgParam.Type.BaseTypeDecl.QualifiedCppName,
        _writer.emitCppGetImpl(delgParamNames[i]);
#endif
                _writer.Write("SWAGGY;");

                classVarMap.Add(delgParam, implVarName);
            }

            ++i;
        }

        // Start calling the C function pointer.
        string delgTypeStr = CppCodeGenerator.GetTypeName(_module, param.Type,
          CppCodeGenerator.NameContext.Neutral, CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        _writer.Write($"(*reinterpret_cast<{delgTypeStr}*>({sysValueVarName}))(");

        // First argument (sys value) is always null.
        _writer.Write("nullptr");
        if (delg.Parameters.Any())
            _writer.Write(", ");

        // Arguments for the C function pointer call.
        i = 0;
        foreach (FunctionParamDecl delgParam in delg.Parameters)
        {
            string delgParamName = delgParamNames[i];

            if (classVarMap.TryGetValue(delgParam, out string? it))
                delgParamName = it;

            {
                string typeNameToCastTo = string.Empty;
                {
                    if (delgParam.Type is TypeDecl typeDecl)
                        typeNameToCastTo = CCodeGenerator.GetTypeName(typeDecl, CCodeGenerator.NameContext.Neutral);
                }

                if (delgParam.Type.IsArray)
                {
                    // Pass an array view.
                    if (!string.IsNullOrEmpty(typeNameToCastTo))
                        _writer.Write($"reinterpret_cast<const {typeNameToCastTo}*>({delgParamName}.data())");
                    else
                        _writer.Write($"{delgParamName}.data()");

                    _writer.Write($", static_cast<uint32_t>({delgParamName}.size())");
                }
                else
                {
                    if (!string.IsNullOrEmpty(typeNameToCastTo))
                    {
                        bool canUseStaticCast = delgParam.Type.IsClass;

                        _writer.Write(canUseStaticCast ? "static_cast<" : "reinterpret_cast<");

                        bool isConst = !delgParam.Type.IsClass;
                        bool needAddress = !delgParam.Type.IsClass;

                        if (isConst)
                            _writer.Write("const ");

                        _writer.Write($"{typeNameToCastTo}*>(");

                        if (needAddress)
                            _writer.Write('&');

                        _writer.Write($"{delgParamName})");
                    }
                    else
                    {
                        _writer.Write(delgParamName);
                    }
                }
            }

            if (delgParam != delg.Parameters[^1])
                _writer.Write(", ");

            ++i;
        }

        if (isReturnValueCast)
            _writer.Write(')');

        _writer.WriteLine(");");
        _writer.Unindent();
        _writer.Write('}');
    }

    private void GenerateFunctionCall_ReturnsClass()
    {
        if (_function.ReturnType.IsArray)
        {
            // Returns an array of classes.
            DeclareVars();

            string thisObjPrefix = GetThisObjPrefix();

            _writer.Write($"{thisObjPrefix}{_function.Name}(");
            GenerateArguments();
            _writer.WriteLine(");");

            Closure();
        }
        else
        {
            // Returns a single class.
            DeclareVars();

            string returnedObjName = _nameGen.CreateNext();

            _writer.Write($"auto {returnedObjName} = ");

            if (_function.ReturnType.IsEnum)
            {
                string enumNameInC =
                  CCodeGenerator.GetTypeName(_function.ReturnType, CCodeGenerator.NameContext.Neutral);

                _writer.Write($"{enumNameInC}(");
            }

            string thisObjPrefix = GetThisObjPrefix();

            _writer.Write($"{thisObjPrefix}{_function.Name}(");
            GenerateArguments();
            _writer.WriteLine(");");

            string implVarName = _nameGen.CreateNext();
            _writer.WriteLine(
              $"auto {implVarName} = {returnedObjName}.{NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.GetImpl, _module)}();");

            _writer.WriteLine(
              $"{returnedObjName}.{NameResolution.GetSpecialBuiltInFunctionName(SpecialCppBuiltInFunction.DropImpl, _module)}();");

            Closure();

            string typeName =
              CCodeGenerator.GetTypeName(_function.ReturnType, CCodeGenerator.NameContext.Neutral);

            _writer.WriteLine($"return reinterpret_cast<{typeName}*>({implVarName});");
        }
    }

    private void GenerateFunctionCall_ReturnsByValue()
    {
        DeclareVars();

        string returnValueVarName = _nameGen.CreateNext();

        _writer.Write($"auto {returnValueVarName} = ");

        if (_function.ReturnType.IsEnum)
        {
            string enumNameInC =
              CCodeGenerator.GetTypeName(_function.ReturnType, CCodeGenerator.NameContext.Neutral);

            _writer.Write($"{enumNameInC}(");
        }

        string thisObjPrefix = GetThisObjPrefix();

        _writer.Write($"{thisObjPrefix}{_function.Name}(");
        GenerateArguments();
        _writer.Write(")");

        if (_function.ReturnType.IsEnum)
            _writer.Write(")");

        _writer.WriteLine(";");

        Closure();

        if (_function.ReturnType == PrimitiveType.Bool)
            _writer.WriteLine($"return static_cast<bool32_t>({returnValueVarName});");
        else
            _writer.WriteLine($"return {returnValueVarName};");
    }

    private void GenerateFunctionCall_ReturnsViaOutParameter()
    {
        DeclareVars();

        if (_function.ReturnType.IsArray)
        {
            string thisObjPrefix = GetThisObjPrefix();
            _writer.Write($"{thisObjPrefix}{_function.Name}(");
            GenerateArguments();
            _writer.WriteLine(");");
        }
        else
        {
            string returnValueVarName = _nameGen.CreateNext();
            _writer.Write($"auto {returnValueVarName} = ");

            string thisObjPrefix = GetThisObjPrefix();

            _writer.Write($"{thisObjPrefix}{_function.Name}(");
            GenerateArguments();
            _writer.WriteLine(");");

            string derefPrefix = "*";

            if (_function.ReturnType.IsClass)
                derefPrefix = string.Empty;

            string returnTypeName =
              CCodeGenerator.GetTypeName(_function.ReturnType, CCodeGenerator.NameContext.Neutral);

            _writer.WriteLine(
              $"*result = {derefPrefix}reinterpret_cast<const {returnTypeName}*>(&{returnValueVarName});");
        }

        Closure();
    }

    private void GenerateFunctionCall_ReturnsNothing()
    {
        DeclareVars();

        string thisObjPrefix = GetThisObjPrefix();

        _writer.Write($"{thisObjPrefix}{_function.Name}(");
        GenerateArguments();
        _writer.WriteLine(");");

        Closure();
    }

    private string GetThisObjPrefix()
    {
        return !_function.HasThisParam
                    ? $"{_module.Name}.{_function.ParentTypeDecl!.Name}."
                    : _thisVarName + '.';
    }
}