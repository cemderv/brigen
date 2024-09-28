using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

/// <summary>
/// Handles calls from C++ JNI to the library's C++ API.
/// </summary>
internal sealed class JniToCppFuncCaller
{
    private readonly Module _module;
    private readonly FunctionDecl _function;
    private readonly TempVarNameGen _nameGen;
    private readonly VarCache<ClassVar> _classVars;
    private readonly VarCache<StructVar> _structVars;
    private readonly VarCache<StringVar> _stringVars;
    private readonly VarCache<ArrayVar> _arrayVars;
    private readonly Writer _writer;

    public JniToCppFuncCaller(Writer writer, TempVarNameGen nameGen, FunctionDecl function)
    {
        Debug.Assert(function.Module != null);

        _writer = writer;
        _nameGen = nameGen;
        _classVars = new VarCache<ClassVar>();
        _structVars = new VarCache<StructVar>();
        _stringVars = new VarCache<StringVar>();
        _arrayVars = new VarCache<ArrayVar>();
        _module = function.Module;
        _function = function;
    }

    public void GenerateCall(string objName)
    {
        ClassDecl funcClass = _function.ParentAsClass!;

        string funcClassNameInCpp =
          CppCodeGenerator.GetTypeName(_module, funcClass, CppCodeGenerator.NameContext.Neutral,
            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        DeclareVars();

        if (_function.IsCtor)
        {
            CallCtor(funcClassNameInCpp);
        }
        else
        {
            CallNormalFunction(objName);
        }
    }

    private void CallCtor(string funcClassNameInCpp)
    {
        string objVarName = _nameGen.CreateNext();

        _writer.Write($"auto {objVarName} = {funcClassNameInCpp}::{_function.Name}(");

        GenerateArguments();
        _writer.WriteLine(");");

        string objImplVarName = _nameGen.CreateNext();
        _writer.WriteLine($"auto {objImplVarName} = {objVarName}.DropImpl();");
        Closure();
        _writer.WriteLine($"return reinterpret_cast<jlong>({objImplVarName});");
    }

    private void CallNormalFunction(string objName)
    {
        IDataType returnType = _function.ReturnType;
        string returnVarName = string.Empty;

        if (_function.HasNonVoidReturnType)
        {
            returnVarName = _nameGen.CreateNext();

            // If the return type is a class, don't bind the variable as const,
            // because we have to drop the variable's impl pointer (call DropImpl), which is a non-const operation.
            if (returnType is not ClassDecl)
                _writer.Write("const ");

            _writer.Write($"auto {returnVarName} = ");
        }

        if (!string.IsNullOrEmpty(objName))
            _writer.Write($"{objName}.");

        _writer.Write(_function.Name + '(');
        GenerateArguments();
        _writer.WriteLine(");");

        Closure();

        if (_function.HasNonVoidReturnType)
            ReturnValue(returnType, returnVarName);
    }

    private void ReturnValue(IDataType returnType, string returnVarName)
    {
        if (returnType is ArrayType arrayType)
        {
            var elemType = arrayType.ElementType;
            if (elemType is StructDecl strct)
                ReturnArrayOfStructs(elemType, returnVarName, strct);
            else if (elemType is ClassDecl)
                ReturnArrayOfClasses(elemType, returnVarName);
            else if (elemType == PrimitiveType.String)
                ReturnArrayOfStrings(returnVarName);
            else if (elemType is PrimitiveType primType)
                ReturnArrayOfPrimitiveType(returnVarName, primType);
            else
                _writer.WriteLine($"return NOTIMPLEMENTED; // TODO: array type '{arrayType.Name}'");
        }
        else if (returnType is StructDecl)
        {
            ReturnSingleStruct(returnType, returnVarName);
        }
        else if (returnType is ClassDecl)
        {
            ReturnSingleClass(returnVarName);
        }
        else if (returnType == PrimitiveType.String)
        {
            ReturnString(returnVarName);
        }
        else if (returnType == PrimitiveType.Byte)
        {
            _writer.WriteLine($"return static_cast<jbyte>({returnVarName});");
        }
        else if (returnType == PrimitiveType.Handle)
        {
            _writer.WriteLine($"return reinterpret_cast<jlong>({returnVarName});");
        }
        else
        {
            _writer.WriteLine($"return {returnVarName};");
        }
    }

    private void ReturnArrayOfStructs(IDataType returnType, string returnVarName, StructDecl strct)
    {
        string jobjectArrayName = _nameGen.CreateNext();
        _writer.WriteLine(
          $"const auto {jobjectArrayName} = env->NewObjectArray(static_cast<jsize>({returnVarName}.size()), {JavaCodeGenerator.CidsName}.{strct.Name}, nullptr);");

        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}.size(); ++{0}) ", loopCounterVarName, returnVarName));
        _writer.OpenBrace();
        {
            string elementVarName =
              CreateJObjectFromStruct(returnType, $"{returnVarName}[{loopCounterVarName}]");

            _writer.WriteLine(
              $"env->SetObjectArrayElement({jobjectArrayName}, static_cast<jsize>({loopCounterVarName}), {elementVarName});");
        }
        _writer.CloseBrace();

        _writer.WriteLine($"return {jobjectArrayName};");
    }

    private void ReturnSingleStruct(IDataType returnType, string returnVarName)
    {
        string resultVarName = CreateJObjectFromStruct(returnType, returnVarName);
        _writer.WriteLine($"return {resultVarName};");
    }

    private void ReturnArrayOfClasses(IDataType returnType, string returnVarName)
        => throw new NotImplementedException();

    private void ReturnSingleClass(string returnVarName)
    {
        var implVarName = _nameGen.CreateNext();
        _writer.WriteLine($"const auto {implVarName} = {returnVarName}.GetImpl();");
        _writer.WriteLine($"{returnVarName}.DropImpl();");
        _writer.WriteLine($"return reinterpret_cast<jlong>({implVarName});");
    }

    private void ReturnArrayOfStrings(string returnVarName)
    {
        string arrSizeName = _nameGen.CreateNext();
        string jniArrName = _nameGen.CreateNext();

        _writer.WriteLine($"const auto {arrSizeName} = {returnVarName}.size();");

        string javaStringClassName = $"{JavaCodeGenerator.CidsName}.{Strings.ForbiddenIdentifierPrefix}JavaString";
        _writer.WriteLine(
          $"const auto {jniArrName} = env->NewObjectArray(static_cast<jsize>({arrSizeName}), {javaStringClassName}, nullptr);");

        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        {
            string jStringVarName = _nameGen.CreateNext();
            _writer.WriteLine($"const auto {jStringVarName} = env->NewStringUTF({returnVarName}[{loopCounterVarName}]);");
            _writer.WriteLine(
              $"env->SetObjectArrayElement({jniArrName}, static_cast<jsize>({loopCounterVarName}), {jStringVarName});");
        }
        _writer.CloseBrace();

        _writer.WriteLine($"return {jniArrName};");
    }

    private void ReturnArrayOfPrimitiveType(string returnVarName, PrimitiveType primType)
    {
        if (primType == PrimitiveType.String)
        {
            ReturnArrayOfStrings(returnVarName);
        }
        else
        {
            JniArrayConvRoutine? info = null;

            if (primType == PrimitiveType.Bool)
                info = _boolJniArrayConvRoutine;
            else if (!_primTypeToJniGetArrayElemsTable.TryGetValue(primType, out info))
                throw CompileError.Internal("unknown primitive type");

            Debug.Assert(info != null);

            string jniArrName = _nameGen.CreateNext();
            string jniArrPtrName = _nameGen.CreateNext();
            string arrSizeName = _nameGen.CreateNext();

            _writer.WriteLine($"const auto {arrSizeName} = {returnVarName}.size();");
            _writer.WriteLine($"const auto {jniArrName} = env->{info.NewArrFunc}(static_cast<jsize>({arrSizeName}));");
            _writer.WriteLine($"const auto {jniArrPtrName} = env->{info.GetElementsFunc}({jniArrName}, nullptr);");

            string loopCounterVarName = _nameGen.CreateNext();
            string jniValueTypeName =
              JavaCodeGenerator.GetTypeName(primType, JavaCodeGenerator.NameContext.JniFunctionReturnType);

            _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
            _writer.OpenBrace();
            {
                string whichKindOfCast = primType == PrimitiveType.Handle ? "reinterpret_cast" : "static_cast";

                _writer.WriteLine(
                  $"{jniArrPtrName}[{loopCounterVarName}] = {whichKindOfCast}<{jniValueTypeName}>({returnVarName}[{loopCounterVarName}]);");
            }
            _writer.CloseBrace();

            _writer.WriteLine($"return {jniArrName};");
        }
    }

    private void ReturnString(string returnVarName)
        => _writer.WriteLine($"return env->NewStringUTF({returnVarName});");

    private void DeclareStructArgVars(FunctionParamDecl param, StructDecl strct)
    {
        string strctNameInCpp =
          CppCodeGenerator.GetTypeName(_module, strct, CppCodeGenerator.NameContext.Neutral,
            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        // A single struct
        var var = new StructVar(_nameGen.CreateNext());
        _writer.WriteLine(
          $"auto {var.Name} = brigen_jobject_Convert<{strctNameInCpp}>(env, {param.Name});");
        _structVars.Add(param, var);
    }

    private void DeclareClassArgVars(FunctionParamDecl param, ClassDecl clss)
    {
        var implVarName = _nameGen.CreateNext();
        var objVarName = _nameGen.CreateNext();
        _classVars.Add(param, new ClassVar(objVarName));

        string classNameInCpp = CppCodeGenerator.GetTypeName(_module, clss, CppCodeGenerator.NameContext.Neutral,
          CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        _writer.Write($"const auto {implVarName} = reinterpret_cast<{classNameInCpp}Impl*>(");
        _writer.Write($"env->GetLongField({param.Name}, {JavaCodeGenerator.FidsName}.{clss.Name}_ptr)");
        _writer.WriteLine(");");
        _writer.WriteLine($"{classNameInCpp} {objVarName}{{{implVarName}}};");
    }

    private void DeclareStringArgVars(FunctionParamDecl param)
    {
        var var = new StringVar(_nameGen.CreateNext());
        _writer.WriteLine($"auto {var.UtfCharsVar} = env->GetStringUTFChars({param.Name}, nullptr);");
        _stringVars.Add(param, var);
    }

    private void DeclareStructArrayArgVars(FunctionParamDecl param, StructDecl strct)
    {
        string strctNameInCpp =
          CppCodeGenerator.GetTypeName(_module, strct, CppCodeGenerator.NameContext.Neutral,
            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        // Array of structs
        string arrName = _nameGen.CreateNext();
        string arrSizeName = _nameGen.CreateNext();

        // Get the array size.
        _writer.WriteLine(
          $"const auto {arrSizeName} = static_cast<size_t>(env->GetArrayLength({param.Name}));");

        // Allocate the native array.
        _writer.WriteLine(
          $"const auto {arrName} = std::make_unique<{strctNameInCpp}[]>({arrSizeName});");

        // Loop through the Java array and convert and copy all elements to our native
        // array.
        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        {
            // Get the Java array element.
            string convertedObjVarName = _nameGen.CreateNext();
            _writer.WriteLine(
              $"const auto {convertedObjVarName} = env->GetObjectArrayElement({param.Name}, static_cast<jsize>({loopCounterVarName}));");

            // Convert and assign it to our native array.
            _writer.WriteLine(
              $"{arrName}[{loopCounterVarName}] = brigen_jobject_Convert<{strctNameInCpp}>(env, {convertedObjVarName});");
        }
        _writer.CloseBrace();

        _arrayVars.Add(param, new ArrayVar($"{arrName}.get()", $"static_cast<uint32_t>({arrSizeName})"));
    }

    private void DeclareClassArrayArgVars(FunctionParamDecl param, ClassDecl clss)
    {
        /*
            for (size_t brigen_4 = 0u; brigen_4 < brigen_3; ++brigen_4)
            {
              const auto brigen_6 = env->GetLongField(brigen_5, brigen_fids.SomeClass_ptr);
              brigen_2[brigen_4] = MyLib::SomeClass(reinterpret_cast<MyLib::SomeClassImpl*>(brigen_6));
            }
         */

        string classNameInCpp =
          CppCodeGenerator.GetTypeName(_module, clss, CppCodeGenerator.NameContext.Neutral,
            CppCodeGenerator.NameContextFlags.ForceModulePrefix);

        string arrName = _nameGen.CreateNext();
        string arrSizeName = _nameGen.CreateNext();

        // Get the array size.
        _writer.WriteLine(
          $"const auto {arrSizeName} = static_cast<size_t>(env->GetArrayLength({param.Name}));");

        // Allocate the native array.
        _writer.WriteLine(
          $"const auto {arrName} = std::make_unique<{classNameInCpp}[]>({arrSizeName});");

        // Loop through the Java array and convert and copy all elements to our native array.
        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0})", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        {
            // Get the Java array element.
            string convertedObjVarName = _nameGen.CreateNext();
            _writer.WriteLine(
              $"const auto {convertedObjVarName} = env->GetObjectArrayElement({param.Name}, static_cast<jsize>({loopCounterVarName}));");

            string longFieldVarName = _nameGen.CreateNext();
            _writer.WriteLine(
              $"const auto {longFieldVarName} = env->GetLongField({convertedObjVarName}, {JavaCodeGenerator.FidsName}.{clss.Name}_ptr);");

            // Convert and assign it to our native array.
            _writer.WriteLine(
              $"{arrName}[{loopCounterVarName}] = {classNameInCpp}(reinterpret_cast<{classNameInCpp}Impl*>({longFieldVarName}));");
        }
        _writer.CloseBrace();

        _arrayVars.Add(param, new ArrayVar($"{arrName}.get()", $"static_cast<uint32_t>({arrSizeName})"));
    }

    private void DeclareStringArrayArgVars(FunctionParamDecl param)
    {
        string arrName = _nameGen.CreateNext();
        string arrSizeName = _nameGen.CreateNext();
        string jstringArrName = _nameGen.CreateNext();

        // Get the array size.
        _writer.WriteLine(
          $"const auto {arrSizeName} = static_cast<size_t>(env->GetArrayLength({param.Name}));");

        // Allocate the native array.
        _writer.WriteLine($"const auto {arrName} = std::make_unique<const char*[]>({arrSizeName});");
        _writer.WriteLine($"const auto {jstringArrName} = std::make_unique<jstring[]>({arrSizeName});");

        // Loop through the Java array and convert and copy all elements to our native array.
        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        {
            // Get the Java array element.
            _writer.Write($"{jstringArrName}[{loopCounterVarName}] = static_cast<jstring>(env->GetObjectArrayElement(");
            _writer.Write($"{param.Name}, static_cast<jsize>({loopCounterVarName})");
            _writer.WriteLine("));");

            // Convert and assign it to our native array.
            _writer.WriteLine(
              $"{arrName}[{loopCounterVarName}] = env->GetStringUTFChars({jstringArrName}[{loopCounterVarName}], nullptr);");
        }
        _writer.CloseBrace();

        string scopeGuardVarName = _nameGen.CreateNext();
        _writer.Write($"const auto {scopeGuardVarName} = ");
        CppCodeGenerator.BeginScopeGuardBlock(_module, _writer);
        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        _writer.WriteLine(
          $"env->ReleaseStringUTFChars({jstringArrName}[{loopCounterVarName}], {arrName}[{loopCounterVarName}]);");
        _writer.CloseBrace();
        CppCodeGenerator.EndScopeGuardBlock(_writer);

        _arrayVars.Add(param, new ArrayVar($"{arrName}.get()", $"static_cast<uint32_t>({arrSizeName})"));
    }

    private void DeclareBoolArrayArgVars(FunctionParamDecl param)
    {
        string jbooleansPtrName = _nameGen.CreateNext();
        string arrName = _nameGen.CreateNext();
        string arrSizeName = _nameGen.CreateNext();

        // Get pointer to contiguous jbooleans.
        _writer.WriteLine($"const auto {jbooleansPtrName} = env->GetBooleanArrayElements({param.Name}, nullptr);");

        // Get the array size.
        _writer.WriteLine(
          $"const auto {arrSizeName} = static_cast<size_t>(env->GetArrayLength({param.Name}));");

        // Allocate the native array.
        _writer.WriteLine(
          $"const auto {arrName} = std::make_unique<{Strings.CppBool32TypeName}[]>({arrSizeName});");

        // Loop through the Java array and convert and copy all elements to our native
        // array.
        string loopCounterVarName = _nameGen.CreateNext();

        _writer.Write(string.Format("for (size_t {0} = 0u; {0} < {1}; ++{0}) ", loopCounterVarName, arrSizeName));
        _writer.OpenBrace();
        _writer.WriteLine($"{arrName}[{loopCounterVarName}] = {jbooleansPtrName}[{loopCounterVarName}];");
        _writer.CloseBrace();

        _arrayVars.Add(param, new ArrayVar($"{arrName}.get()", $"static_cast<uint32_t>({arrSizeName})"));
    }

    private void DeclarePrimitiveTypeArrayArgVars(FunctionParamDecl param, PrimitiveType primType)
    {
        if (primType == PrimitiveType.String)
        {
            // Strings need special handling because of their reference semantics.
            DeclareStringArrayArgVars(param);
        }
        else if (primType == PrimitiveType.Bool)
        {
            // Bools also need special handling, because they're 1 byte in Java and 4 byte in (our) C++.
            DeclareBoolArrayArgVars(param);
        }
        else
        {
            if (!_primTypeToJniGetArrayElemsTable.TryGetValue(primType, out var info))
                throw CompileError.Internal("unknown primitive type");

            string ptrName = _nameGen.CreateNext();
            string sizeName = _nameGen.CreateNext();

            _writer.WriteLine($"const auto {ptrName} = env->{info.GetElementsFunc}({param.Name}, nullptr);");
            _writer.WriteLine($"const auto {sizeName} = env->GetArrayLength({param.Name});");

            _arrayVars.Add(param, new ArrayVar($"reinterpret_cast<{info.CppPtrType}>({ptrName})", sizeName));
        }
    }

    private void DeclareArrayArgVars(FunctionParamDecl param, ArrayType arrayType)
    {
        var elemType = arrayType.ElementType;

        if (elemType is StructDecl strct)
        {
            DeclareStructArrayArgVars(param, strct);
        }
        else if (elemType is ClassDecl clss)
        {
            DeclareClassArrayArgVars(param, clss);
        }
        else if (elemType is PrimitiveType primType)
        {
            DeclarePrimitiveTypeArrayArgVars(param, primType);
        }
        else
        {
            throw CompileError.Internal("unknown array type");
        }
    }

    private void DeclareVars()
    {
        foreach (FunctionParamDecl param in _function.Parameters)
        {
            IDataType paramType = param.Type;

            if (paramType is StructDecl strct)
            {
                DeclareStructArgVars(param, strct);
            }
            else if (paramType is ClassDecl clss)
            {
                DeclareClassArgVars(param, clss);
            }
            else if (paramType == PrimitiveType.String)
            {
                DeclareStringArgVars(param);
            }
            else if (paramType is ArrayType arrayType)
            {
                DeclareArrayArgVars(param, arrayType);
            }
        }
    }

    private void GenerateArguments()
    {
        foreach (FunctionParamDecl param in _function.Parameters)
        {
            IDataType paramType = param.Type;

            if (_stringVars.TryGet(param, out StringVar strVar))
            {
                if (paramType.IsArray)
                {
                    // Array of strings
                    throw new NotImplementedException();
                }
                else
                {
                    // A single string
                    _writer.Write(strVar.UtfCharsVar);
                }
            }
            else if (_structVars.TryGet(param, out StructVar strctVar))
            {
                // Single struct argument
                _writer.Write(strctVar.Name);
            }
            else if (_classVars.TryGet(param, out ClassVar clssVar))
            {
                // Single class argument
                _writer.Write(clssVar.Name);
            }
            else if (_arrayVars.TryGet(param, out ArrayVar arrayVar))
            {
                // Array argument
                _writer.Write($"{arrayVar.PtrName}, {arrayVar.SizeName}");
            }
            else if (paramType == PrimitiveType.Handle)
            {
                _writer.Write($"reinterpret_cast<void*>({param.Name})");
            }
            else if (paramType is DelegateDecl)
            {
                _writer.Write("/*TODO: delegate arguments*/ {}, nullptr");
            }
            else
            {
                _writer.Write(param.Name);
            }

            if (param != _function.Parameters[^1])
                _writer.Write(", ");
        }
    }

    private void Closure()
    {
        foreach (FunctionParamDecl param in _function.Parameters.Reverse())
        {
            IDataType paramType = param.Type;

            if (paramType == PrimitiveType.String)
            {
                bool result = _stringVars.TryGet(param, out StringVar var);
                Debug.Assert(result);
                _writer.WriteLine($"env->ReleaseStringUTFChars({param.Name}, {var.UtfCharsVar});");
            }
        }
    }

    private string CreateJObjectFromStruct(IDataType structType, string strctObjName)
    {
        var strct = structType as StructDecl;
        Debug.Assert(strct != null);

        string jobjectName = _nameGen.CreateNext();

        _writer.WriteLine(
          $"const auto {jobjectName} = env->NewObject({JavaCodeGenerator.CidsName}.{strct.Name}, {JavaCodeGenerator.MidsName}.{strct.Name}{JavaCodeGenerator.CtorSuffix});");

        foreach (StructFieldDecl field in strct.Fields)
        {
            IDataType fieldType = field.Type;
            string fieldName = $"{strctObjName}.{field.Name}";

            if (fieldType is StructDecl)
            {
                string newFieldName = CreateJObjectFromStruct(fieldType, fieldName);
                _writer.Write("env->SetObjectField(");
                fieldName = newFieldName;
            }
            else if (fieldType == PrimitiveType.Byte)
                _writer.Write("env->SetByteField(");
            else if (fieldType == PrimitiveType.Int)
                _writer.Write("env->SetIntField(");
            else if (fieldType == PrimitiveType.Short)
                _writer.Write("env->SetShortField(");
            else if (fieldType == PrimitiveType.Long || fieldType == PrimitiveType.Handle)
                _writer.Write("env->SetLongField(");
            else if (fieldType == PrimitiveType.Bool)
                _writer.Write("env->SetBooleanField(");
            else if (fieldType == PrimitiveType.Float)
                _writer.Write("env->SetFloatField(");
            else if (fieldType == PrimitiveType.Double)
                _writer.Write("env->SetDoubleField(");
            else
                throw CompileError.Internal($"invalid inner type \"{structType.Name}\"", field.Range);

            _writer.Write(
              $"{jobjectName}, {JavaCodeGenerator.FidsName}.{strct.Name}_{field.NameInJava}, ");

            bool isCasted = false;

            if (fieldType == PrimitiveType.Handle)
            {
                _writer.Write("reinterpret_cast<jlong>(");
                isCasted = true;
            }
            else if (fieldType == PrimitiveType.Byte)
            {
                // jbyte is 'signed char', but we use uint8_t in native code, which is unsigned.
                // We have to perform a static cast here.
                _writer.Write("static_cast<jbyte>(");
                isCasted = true;
            }

            _writer.Write(fieldName);
            _writer.Write(')');

            if (isCasted)
                _writer.Write(')');

            _writer.WriteLine(';');
        }

        return jobjectName;
    }

    private record StringVar(string UtfCharsVar);

    private record StructVar(string Name);

    private record ClassVar(string Name);

    private record ArrayVar(string PtrName, string SizeName);

    private class VarCache<T>
    {
        private readonly Dictionary<FunctionParamDecl, T> _vars = [];

        public void Add(FunctionParamDecl param, T var) => _vars.Add(param, var);

        public bool TryGet(FunctionParamDecl param, out T result) => _vars.TryGetValue(param, out result!);
    }

    private record JniArrayConvRoutine(string GetElementsFunc, string CppPtrType, string NewArrFunc);

    /// <summary>
    /// Table that maps primitive types to their corresponding Get...ArrayElements routines.
    /// </summary>  
    private static readonly Dictionary<PrimitiveType, JniArrayConvRoutine>
      _primTypeToJniGetArrayElemsTable = new()
      {
      { PrimitiveType.Byte, new("GetByteArrayElements", "const uint8_t*", "NewByteArray") },
      { PrimitiveType.Int, new("GetIntArrayElements", "const int32_t*", "NewIntArray") },
      { PrimitiveType.Short, new("GetShortArrayElements", "const int16_t*", "NewShortArray") },
      { PrimitiveType.Long, new("GetLongArrayElements", "const int64_t*", "NewLongArray") },
      { PrimitiveType.Float, new("GetFloatArrayElements", "const float*", "NewFloatArray") },
      { PrimitiveType.Double, new("GetDoubleArrayElements", "const double*", "NewDoubleArray") },
      { PrimitiveType.Handle, new("GetLongArrayElements", "const void* const*", "NewLongArray") },
      };

    private static readonly JniArrayConvRoutine _boolJniArrayConvRoutine =
      new("GetBooleanArrayElements", "const bool32_t*", "NewBooleanArray");
}