using brigen.decl;
using brigen.types;

namespace brigen;

internal sealed class ModuleVerifier
{
    private static readonly string[] _reservedTypeNames = [];

    public void VerifyModule(Module module)
    {
        foreach (Decl decl in module.Decls)
            decl.Verify(module);

        // See if any decl is named after a reserved type.
        foreach (Decl decl in module.Decls)
            if (_reservedTypeNames.Contains(decl.Name))
                throw new CompileError($"Type '{decl.Name}' is named after a reserved type.", decl.Range);

        // See if there are any duplicate top-level declarations.
        {
            var uniqueNames = new SortedSet<string>();

            foreach (Decl decl in module.Decls)
                if (!uniqueNames.Add(decl.Name))
                    throw new CompileError($"A symbol named '{decl.Name}' exists multiple times.", decl.Range);
        }

        foreach (Decl decl in module.Decls)
            _ = decl switch
            {
                EnumDecl enm => VerifyEnum(enm),
                StructDecl strct => VerifyStruct(strct),
                ClassDecl clss => VerifyClass(clss),
                DelegateDecl delg => VerifyDelegate(delg),
                _ => -1
            };
    }

    private static int VerifyEnum(EnumDecl enm)
    {
        foreach (EnumMemberDecl memberA in enm.Members)
        {
            foreach (EnumMemberDecl memberB in enm.Members)
            {
                if (memberA == memberB)
                    continue;

                if (memberA.Name == memberB.Name)
                    throw new CompileError($"Duplicate member \"{memberA.Name}\" in enum \"{enm.Name}\".", enm.Range);
            }
        }

        return 0;
    }

    private static int VerifyStruct(StructDecl strct)
    {
        // Check duplicate fields.
        foreach (StructFieldDecl fieldA in strct.Fields)
        {
            foreach (StructFieldDecl fieldB in strct.Fields)
            {
                if (fieldA == fieldB)
                    continue;

                if (fieldA.Name == fieldB.Name)
                    throw new CompileError($"Duplicate field \"{fieldA.Name}\" in struct \"{strct.Name}\".", strct.Range);
            }
        }

        foreach (StructFieldDecl field in strct.Fields)
        {
            // Check void fields.
            if (field.Type == PrimitiveType.Void)
                throw new CompileError($"Field \"{field.Name}\" is of type void, which is not allowed.", field.Range);

            // Check if it's a delegate, which is not allowed.
            if (field.Type.IsDelegate)
                throw new CompileError($"Field \"{field.Name}\" is of type delegate, which is not allowed.", field.Range);
        }

        return 0;
    }

    private static int VerifyClass(ClassDecl clss)
    {
        var functionNameSet = new SortedSet<string>();

        foreach (FunctionDecl func in clss.AllExportedFunctions)
        {
            if (!functionNameSet.Add(func.Name))
            {
                // See if the function belongs to a property.
                // If so, the error message is a different one.
                if (func.OriginalProperty != null)
                    throw new CompileError(
                      $"Class \"{clss.Name}\" declares a property named \"{func.OriginalProperty.Name}\" multiple times.",
                      func.Range);

                throw new CompileError(
                  $"Class \"{clss.Name}\" declares a function named \"{func.Name}\" multiple times.", func.Range);
            }

            VerifyClassFunction(func);
        }

        foreach (FunctionDecl func in clss.AllExportedFunctions)
            // Returning delegates is not allowed.
            if (func.HasNonVoidReturnType && func.ReturnType.IsDelegate)
                throw new CompileError("Delegates cannot be used as return types.", func.Range);

        // A static class can have only static functions/props and no ctors.
        if (clss.IsStatic)
        {
            if (clss.Ctors.Any()) throw new CompileError("Static classes cannot have any constructors.", clss.Range);

            foreach (FunctionDecl func in clss.Functions)
                if (!func.IsStatic)
                    throw new CompileError("Static classes may only have static functions.", func.Range);

            foreach (PropertyDecl prop in clss.Properties)
                if (!prop.IsStatic)
                    throw new CompileError("Static classes may only have static properties.", prop.Range);
        }

        return 0;
    }

    private static void VerifyClassFunction(FunctionDecl function)
    {
        // A function cannot be a constructor and static at the same time.
        if (function.IsCtor && function.IsStatic)
            throw new CompileError("A constructor cannot be declared as static.", function.Range);

        // Detect duplicate-named parameters.
        foreach (FunctionParamDecl paramA in function.Parameters)
        {
            foreach (FunctionParamDecl paramB in function.Parameters)
            {
                if (paramA == paramB)
                    continue;

                if (paramA.Name == paramB.Name)
                    throw new CompileError(
                      $"Duplicate parameter \"{paramA.Name}\" in function \"{function.Name}\".", function.Range);
            }
        }
    }

    private static int VerifyDelegate(DelegateDecl delg)
    {
        // Detect duplicate-named parameters.
        foreach (FunctionParamDecl paramA in delg.Parameters)
        {
            foreach (FunctionParamDecl paramB in delg.Parameters)
            {
                if (paramA == paramB)
                    continue;

                if (paramA.Name == paramB.Name)
                    throw new CompileError(
                      $"Duplicate parameter \"{paramA.Name}\" in delegate \"{delg.Name}\".", paramA.Range);
            }
        }

        // Arrays cannot be returned from delegates.
        if (delg.ReturnType!.IsArray)
            throw new CompileError("Arrays cannot be used as return types by delegates.", delg.Range);

        return 0;
    }
}