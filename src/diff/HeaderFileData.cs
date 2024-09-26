using brigen.decl;
using ClangSharp.Interop;

namespace brigen.diff;

internal sealed class HeaderFileData(DiffDetector diffDetector) : ClangBasedFile(diffDetector)
{
    private static readonly string[] _classesToIgnore = ["ObjectImplBase"];

    public readonly List<ClassInfo> Classes = [];

    public readonly List<string> ForwardDeclarations = [];

    public override unsafe bool Load(string filename)
    {
        if (!base.Load(filename))
            return false;

        Logger.Log("Parsing C++ header\n");

        string currentNamespace = string.Empty;
        var currentClass = new ClassInfo();
        var currentVisibility = Visibility.Public;

        TUnit.Cursor.VisitChildren((c, parent, _) =>
        {
            CXSourceLocation location = c.Location;

            if (!location.IsFromMainFile)
                return CXChildVisitResult.CXChildVisit_Continue;

            CXTranslationUnit cxTUnit = TUnit;
            CXCursorKind cursorKind = c.Kind;
            CXSourceRange range = c.Extent;

            if (cursorKind == CXCursorKind.CXCursor_Namespace)
            {
                CXToken* token = cxTUnit.GetToken(location);
                CXSourceRange tokenRange = token->GetExtent(cxTUnit);
                currentNamespace = GetClangSubstring(cxTUnit, tokenRange);
            }
            else if (cursorKind == CXCursorKind.CXCursor_ClassDecl)
            {
                CXCursor parentCursor = c.SemanticParent;
                CXCursorKind parentCursorKind = parentCursor.Kind;

                if (parentCursorKind == CXCursorKind.CXCursor_ClassDecl)
                {
                    using CXString parentSpelling = parentCursor.Spelling;
                    string parentName = parentSpelling.CString;

                    if (parentName == currentClass.Name)
                    {
                        // This is a nested class, which is not supported ATM.
                        ClangRange clangRange = GetClangRange(cxTUnit, range);

                        throw new CompileError(
                      "Nested classes are not supported yet. Please use a nested struct instead.");
                    }
                }

                if (IsForwardDeclaration(c))
                {
                    string fwdDeclContent = GetClangSubstring(cxTUnit, range);
                    ForwardDeclarations.Add(fwdDeclContent);
                    return CXChildVisitResult.CXChildVisit_Continue;
                }

                CXToken* token = cxTUnit.GetToken(location);
                CXSourceRange tokenRange = token->GetExtent(cxTUnit);

                string className = GetClangSubstring(cxTUnit, tokenRange);

                // See if this class has to be ignored.
                // This is the case for built-in classes where we don't need to extract
                // any information, such as ObjectImplBase.
                if (_classesToIgnore.Contains(className))
                    return CXChildVisitResult.CXChildVisit_Continue;

                if (currentClass.Functions.Count > 0)
                {
                    Classes.Add(currentClass);
                    currentClass = new ClassInfo();
                }

                currentClass.Namespace = currentNamespace;
                currentClass.Name = className;

#if LOG_CLANG_AST
          Console.WriteLine($"New class: {className}");
#endif

                currentClass.Content = GetClangSubstring(cxTUnit, range);

                // This will be determined later.
                currentClass.RepresentedClassDecl = null;

                return CXChildVisitResult.CXChildVisit_Recurse;
            }
            else if (cursorKind == CXCursorKind.CXCursor_EnumDecl)
            {
                if (IsForwardDeclaration(c))
                {
                    string fwdDeclContent = GetClangSubstring(cxTUnit, range);
                    ForwardDeclarations.Add(fwdDeclContent);
                    return CXChildVisitResult.CXChildVisit_Continue;
                }
            }

            // Check if we're currently in a valid class. If not, skip to the next
            // symbol.
            if (string.IsNullOrEmpty(currentClass.Name))
                return CXChildVisitResult.CXChildVisit_Recurse;

            using CXString idSpelling = c.Spelling;

            location.GetExpansionLocation(out CXFile _, out uint cursorLine, out uint _, out uint _);

            CXChildVisitResult resultChildVisit = CXChildVisitResult.CXChildVisit_Recurse;

            if (cursorKind == CXCursorKind.CXCursor_Constructor || cursorKind == CXCursorKind.CXCursor_CXXMethod ||
            cursorKind == CXCursorKind.CXCursor_Destructor)
            {
                var info = new FunctionInfo
                {
                    Kind = GetFunctionKind(cursorKind),
                    Name = idSpelling.CString,
                    Content = GetClangSubstring(cxTUnit, range)
                };

                info.Content = TakeSemicolonIfPresent(info.Content);

                string signature = ExtractClangFunctionSignature(info.Content, true);

#if DEBUG
                info.Signature = signature;
#endif

                info.SignatureIDString = FunctionDecl.ComputeSignatureIDString(signature);
                info.Visibility = currentVisibility;

                currentClass.Functions.Add(info);
            }
            else if (cursorKind == CXCursorKind.CXCursor_FieldDecl || cursorKind == CXCursorKind.CXCursor_VarDecl)
            {
                var info = new FieldInfo
                {
                    Name = idSpelling.CString,
                    Content = GetClangSubstring(cxTUnit, range),
                    Visibility = currentVisibility
                };

                currentClass.Fields.Add(info);
            }
            else if (cursorKind == CXCursorKind.CXCursor_StructDecl)
            {
                var info = new StructInfo
                {
                    Name = idSpelling.CString,
                    Content = GetClangSubstring(cxTUnit, range),
                    Visibility = currentVisibility
                };

                currentClass.Structs.Add(info);
                resultChildVisit = CXChildVisitResult.CXChildVisit_Continue;
            }
            else if (cursorKind == CXCursorKind.CXCursor_EnumDecl)
            {
                var info = new EnumInfo { Content = GetClangSubstring(cxTUnit, range), Visibility = currentVisibility };

                currentClass.Enums.Add(info);
                resultChildVisit = CXChildVisitResult.CXChildVisit_Continue;
            }
            else if (cursorKind == CXCursorKind.CXCursor_InitListExpr)
            {
                throw new CompileError("Initializer-list expressions are not supported.");
            }
            else if (cursorKind == CXCursorKind.CXCursor_CXXAccessSpecifier)
            {
                CXToken* token = cxTUnit.GetToken(location);
                CXSourceRange tokenRange = token->GetExtent(cxTUnit);
                string visValue = GetClangSubstring(cxTUnit, tokenRange).Trim(' ').Trim(':');

                currentVisibility = visValue switch
                {
                    "public" => Visibility.Public,
                    "protected" => Visibility.Protected,
                    "private" => Visibility.Private,
                    _ => Visibility.Public
                };
            }
            else if (cursorKind == CXCursorKind.CXCursor_FunctionTemplate)
            {
                throw new CompileError(
              $"The class \"{currentClass.Name}\" declares a function template named \"{idSpelling.CString}\". Function templates are currently not supported.");
            }

            return resultChildVisit;
        }, default);

        if (currentClass.Functions.Count > 0)
        {
            Classes.Add(currentClass);
            currentClass = new ClassInfo();
        }

        foreach (ClassInfo classInfo in Classes)
        {
            classInfo.RepresentedClassDecl = DiffDetector.FindClassDecl(classInfo.Namespace, classInfo.Name);

            foreach (FunctionInfo funcInfo in classInfo.Functions)
                funcInfo.ParentClass = classInfo.RepresentedClassDecl;

            SortBasedOnVisibility(classInfo.Enums);
            SortBasedOnVisibility(classInfo.Functions);
            SortBasedOnVisibility(classInfo.Fields);
        }

        return true;
    }
}