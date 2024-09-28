using brigen.decl;
using brigen.types;
using System.Diagnostics;

namespace brigen.gen;

internal sealed class CSharpPInvokeFuncCaller
{
    private const string _optValueSuffix = "_OptValue";
    private readonly FunctionDecl _function;
    private readonly List<(FunctionParamDecl, string)> _gcHandles;
    private readonly TempVarNameGen _nameGen;
    private readonly List<string> _parameterlessGcHandles;

    private readonly Writer _writer;
    private bool _isOutParamArrayTypeDifferent;
    private string _outArrayGCHandleVarName = string.Empty;
    private string _outParamVarName = string.Empty;
    private string _outVarArraySizeIntPtrName = string.Empty;
    private string _outVarArraySizeName = string.Empty;

    public CSharpPInvokeFuncCaller(Writer writer, TempVarNameGen nameGen, FunctionDecl function)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _nameGen = nameGen ?? throw new ArgumentNullException(nameof(nameGen));
        _function = function ?? throw new ArgumentNullException(nameof(function));
        _gcHandles = [];
        _parameterlessGcHandles = [];
    }

    public void GenerateCall()
    {
        DeclareGcHandleVars();

        const int bfNone = 0;
        const int bfTry = 1;
        const int bfFinally = 4;

        int DetermineBlockFlags()
        {
            int ret = bfNone;

            if (NeedTryFinallyBlock())
            {
                ret |= bfTry;
                ret |= bfFinally;
            }

            return ret;
        }

        int blockFlags = DetermineBlockFlags();

        if ((blockFlags & bfTry) == bfTry)
        {
            _writer.Write("try\n");
            _writer.OpenBrace();
            TryBlockBeginning();
        }

        if (_function.IsCtor)
        {
            string resultVarName = _nameGen.CreateNext();
            _writer.Write($"var {resultVarName} = NativeFunctions.{_function.NameInC}(");
            GenerateArguments();
            _writer.WriteLine(");");

            _writer.WriteLine($"if ({resultVarName} == IntPtr.Zero)");
            _writer.Indent();
            _writer.WriteLine($"throw new Exception(\"Failed to create the {_function.ParentTypeDecl!.Name}.\");");
            _writer.Unindent();

            _writer.WriteLine($"return new {_function.ParentTypeDecl.Name}({resultVarName});");
        }
        else
        {
            void GenerateCallInternal()
            {
                _writer.Write($"NativeFunctions.{_function.NameInC}(");
                GenerateArguments();
                _writer.WriteLine(");");
            }

            if (_function.HasNonVoidReturnType)
            {
                if (_function.HasOutReturnParam)
                {
                    // Return a value that was passed as an out-parameter to the PInvoke function.
                    GenerateCallInternal();
                    Debug.Assert(_outParamVarName.Any());

                    if (_isOutParamArrayTypeDifferent)
                    {
                        // An array of classes is returned. The value _outParamVarName is of type
                        // IntPtr[] at this point, but we have to return real C# instances of the
                        // classes. Therefore we have to allocate a separate array that holds all of
                        // the instances and copy the IntPtrs over.

                        string elemName = string.Empty;
                        string arrElemPrefix = string.Empty;
                        string arrElemSuffix = string.Empty;

                        if (_function.ReturnType is ClassDecl clss)
                        {
                            elemName = clss.Name;
                            arrElemPrefix = $"new {clss.Name}(";
                            arrElemSuffix = ")";
                        }
                        else if (_function.ReturnType == PrimitiveType.Bool)
                        {
                            elemName = "bool";
                            arrElemSuffix = " == 1";
                        }
                        else if (_function.ReturnType == PrimitiveType.String)
                        {
                            elemName = "string";
                            arrElemPrefix = "Marshal.PtrToStringAnsi(";
                            arrElemSuffix = ")";
                        }

                        Debug.Assert(!string.IsNullOrEmpty(elemName));

                        string retArrVarName = _nameGen.CreateNext();
                        _writer.WriteLine($"var {retArrVarName} = new {elemName}[{_outVarArraySizeName}];");

                        // Construct the classes.
                        string loopCounterVarName = _nameGen.CreateNext();
                        _writer.WriteLine(string.Format("foreach (int {0} = 0; {0} < {1}; ++{0})", loopCounterVarName,
                          _outVarArraySizeName));
                        _writer.Indent();
                        _writer.WriteLine(string.Format("{0}[{1}] = {2}{3}[{1}]{4};", retArrVarName,
                          loopCounterVarName, arrElemPrefix,
                          _outParamVarName, arrElemSuffix));
                        _writer.Unindent();

                        _writer.WriteLine($"return {retArrVarName};");
                    }
                    else
                    {
                        _writer.WriteLine($"return {_outParamVarName};");
                    }
                }
                else
                {
                    // Return a value normally.

                    if (_function.ReturnType.IsClass)
                    {
                        // Classes are returned as IntPtr from PInvoke functions.
                        // We have to wrap them in their respective C# type and return that instead.
                        string retVarName = _nameGen.CreateNext();

                        var returnedClass = _function.ReturnType as ClassDecl;

                        Debug.Assert(returnedClass != null);

                        _writer.Write($"var {retVarName} = ");
                        GenerateCallInternal();
                        _writer.Write($"return new {returnedClass.Name}({retVarName})");
                        _writer.WriteLine(";");
                    }
                    else if (_function.ReturnType == PrimitiveType.Bool)
                    {
                        // Bools require special handling, as they're 32-bit in native code, but <
                        // 32-bit in .NET. That's fine, we get them as 32-bit from the PInvoke function,
                        // but we have to convert them here and return the converted bool instead.
                        string retVarName = _nameGen.CreateNext();

                        _writer.Write($"var {retVarName} = ");
                        GenerateCallInternal();
                        _writer.WriteLine($"return {retVarName} == 1;");
                    }
                    else if (_function.ReturnType == PrimitiveType.String)
                    {
                        // Strings require special handling. Strings are returned as pointers
                        // from the C API, which is an IntPtr in C# land. To marshal this
                        // correctly, we have to call Marshal.PtrToStringAnsi().
                        string retVarName = _nameGen.CreateNext();

                        _writer.Write($"var {retVarName} = ");
                        GenerateCallInternal();
                        _writer.WriteLine(
                          $"return {retVarName} != IntPtr.Zero ? Marshal.PtrToStringAnsi({retVarName}) ?? string.Empty : string.Empty;");
                    }
                    else
                    {
                        _writer.Write("return ");
                        GenerateCallInternal();
                    }
                }
            }
            else
            {
                GenerateCallInternal();
            }
        }

        if ((blockFlags & bfTry) == bfTry)
            _writer.CloseBrace(); // Close the try-block.

        // Generate the finally-block.
        if ((blockFlags & bfFinally) == bfFinally)
        {
            _writer.WriteLine("finally");
            _writer.OpenBrace();
            FinallyBlock();
            _writer.CloseBrace();
        }

        Closure();
    }

    private static bool HaveToPinParam(FunctionParamDecl param)
    {
        if (param.Type.IsArray)
        {
            if (param.Type == PrimitiveType.String)
                return false;

            return true;
        }

        return false;
    }


    private void DeclareGcHandleVars()
    {
        _gcHandles.Clear();

        foreach (FunctionParamDecl param in _function.Parameters)
            if (HaveToPinParam(param))
            {
                // We have to pin arrays.
                string varName = _nameGen.CreateNext();

                _writer.WriteLine($"var {varName} = GCHandle.Alloc({param.Name}, GCHandleType.Pinned);");

                _gcHandles.Add((param, varName));
            }

        if (_function.HasOutReturnParam)
        {
            _outArrayGCHandleVarName = _nameGen.CreateNext();

            if (_function.ReturnType.IsArray)
            {
                _writer.WriteLine($"var {_outArrayGCHandleVarName} = default(GCHandle);");
                _parameterlessGcHandles.Add(_outArrayGCHandleVarName);
            }
        }
    }


    private void TryBlockBeginning()
    {
        if (_function.HasOutReturnParam && _function.ReturnType.IsArray)
        {
            _outVarArraySizeName = _nameGen.CreateNext();
            _outVarArraySizeIntPtrName = _nameGen.CreateNext();

            _writer.WriteLine($"var {_outVarArraySizeName} = 0u;");
            _writer.WriteLine($"var {_outVarArraySizeIntPtrName} = new IntPtr(&{_outVarArraySizeName});");

            // Obtain the array size.
            _writer.Write($"NativeFunctions.{_function.NameInC}(");
            GenerateArguments(false);
            _writer.Write($", IntPtr.Zero, {_outVarArraySizeIntPtrName}");
            _writer.WriteLine(");");

            string arrayElementTypeName = CSharpCodeGenerator.GetTypeName(
              _function.ReturnType, CSharpCodeGenerator.NameContext.General,
              CSharpCodeGenerator.GetTypeNameFlags.NoArray | CSharpCodeGenerator.GetTypeNameFlags.NoRef |
              CSharpCodeGenerator.GetTypeNameFlags.NoOut);

            string arrayVarName = _nameGen.CreateNext();

            bool IsOutParamArrayTypeDifferent()
            {
                if (_function.ReturnType.IsClass)
                    return true;

                if (_function.ReturnType == PrimitiveType.Bool)
                    return true;

                if (_function.ReturnType == PrimitiveType.String)
                    return true;

                return false;
            }

            _isOutParamArrayTypeDifferent = IsOutParamArrayTypeDifferent();

            bool hasEarlyOuted = false;

            if (_isOutParamArrayTypeDifferent)
            {
                string elemName = string.Empty;

                if (_function.ReturnType is ClassDecl clss)
                    elemName = clss.Name;
                else if (_function.ReturnType == PrimitiveType.Bool)
                    elemName = "bool";
                else if (_function.ReturnType == PrimitiveType.String) elemName = "string";

                Debug.Assert(!string.IsNullOrEmpty(elemName));

                _writer.WriteLine($"if ({_outVarArraySizeName} == 0)");
                _writer.Indent();
                _writer.WriteLine($"return new {elemName}[0];");
                _writer.Unindent();

                hasEarlyOuted = true;
            }

            // Early-out if the array has a size of 0, but only if we haven't early-outed before.
            if (!hasEarlyOuted)
            {
                _writer.WriteLine($"if ({_outVarArraySizeName} == 0)");
                _writer.Indent();
                _writer.WriteLine($"return Array.Empty<{arrayElementTypeName}>();");
                _writer.Unindent();
            }

            // Allocate the managed array.
            _writer.WriteLine($"var {arrayVarName} = new {arrayElementTypeName}[{_outVarArraySizeName}];");

            // Allocate the array's GC handle.
            _writer.WriteLine($"{_outArrayGCHandleVarName} = GCHandle.Alloc({arrayVarName}, GCHandleType.Pinned);");

            _outParamVarName = arrayVarName;
        }
    }


    private void GenerateArguments(bool allowOutVarFlags = true)
    {
        const int ovfNone = 0;
        const int ovfOutParam = 1;
        const int ovfOutArray = 2;

        int DetermineOutVarFlags()
        {
            if (!allowOutVarFlags)
                return ovfNone;

            if (_function.HasOutReturnParam)
            {
                if (_function.ReturnType.IsArray)
                    return ovfOutArray;

                return ovfOutParam;
            }

            return ovfNone;
        }

        int outVarFlags = DetermineOutVarFlags();

        if (_function.HasThisParam)
        {
            _writer.Write(CSharpCodeGenerator.IntPtrFieldName);

            if (_function.Parameters.Any() || outVarFlags != ovfNone)
                _writer.Write(", ");
        }

        foreach (FunctionParamDecl param in _function.Parameters)
        {
            (FunctionParamDecl, string) it = _gcHandles.Find(t => t.Item1 == param);

            if (it != default)
            {
                // This is a GC handle. Reference it.
                _writer.Write($"{it.Item2}.AddrOfPinnedObject()");

                if (param.Type.IsArray)
                    _writer.Write($", (uint){param.Name}.Length");
            }
            else
            {
                if (param.Type.IsArray && param.Type == PrimitiveType.String)
                {
                    // String arrays can be passed directly to the PInvoke function.
                    _writer.Write(string.Format("{0}, (uint){0}.Length", param.Name));
                }
                else
                {
                    // If the parameter doesn't have a GC handle, it should NOT be an array.
                    // Arrays should always be wrapped in a pinned GC handle.
                    Debug.Assert(!param.Type.IsArray);

                    if (param.Type.IsStruct)
                        _writer.Write("ref ");

                    _writer.Write(param.Name);

                    if (param.Type.IsClass)
                        _writer.Write($".{CSharpCodeGenerator.IntPtrFieldName}");
                }
            }

            if (param != _function.Parameters[^1] || outVarFlags != ovfNone)
                _writer.Write(", ");
        }

        if (outVarFlags == ovfOutParam)
        {
            if (string.IsNullOrEmpty(_outParamVarName))
                _outParamVarName = _nameGen.CreateNext();

            _writer.Write($"out var {_outParamVarName}");
        }
        else if (outVarFlags == ovfOutArray)
        {
            Debug.Assert(!string.IsNullOrEmpty(_outArrayGCHandleVarName));
            Debug.Assert(!string.IsNullOrEmpty(_outVarArraySizeIntPtrName));
            _writer.Write($"{_outArrayGCHandleVarName}.AddrOfPinnedObject(), {_outVarArraySizeIntPtrName}");
        }
    }


    private void Closure()
    {
    }


    private void FinallyBlock()
    {
        for (int i = _gcHandles.Count - 1; i >= 0; --i)
        {
            string varName = _gcHandles[i].Item2;
            _writer.WriteLine($"{varName}.Free();");
        }

        for (int i = _parameterlessGcHandles.Count - 1; i >= 0; --i)
        {
            string varName = _parameterlessGcHandles[i];

            _writer.WriteLine($"if ({varName}.IsAllocated)");
            _writer.Indent();
            _writer.WriteLine($"{varName}.Free();");
            _writer.Unindent();
        }
    }


    private bool NeedTryFinallyBlock() =>
      _gcHandles.Count > 0 || (_function.HasOutReturnParam && _function.ReturnType.IsArray);
}