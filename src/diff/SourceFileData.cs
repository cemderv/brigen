using brigen.decl;
using ClangSharp.Interop;

namespace brigen.diff;

internal sealed class SourceFileData(DiffDetector diffDetector)
    : ClangBasedFile(diffDetector)
{
    public List<FunctionInfo> Functions = [];

    public override unsafe bool Load(string filename)
    {
        if (!base.Load(filename))
            return false;

        Console.WriteLine("Parsing C++ source");

        TUnit.Cursor.VisitChildren((c, parent, _) =>
        {
            CXSourceLocation location = c.Location;

            if (!location.IsFromMainFile)
                return CXChildVisitResult.CXChildVisit_Continue;

            CXCursorKind cursorKind = c.Kind;

            if (cursorKind == CXCursorKind.CXCursor_Namespace)
                return CXChildVisitResult.CXChildVisit_Recurse;

            if (cursorKind != CXCursorKind.CXCursor_Constructor &&
            cursorKind != CXCursorKind.CXCursor_CXXMethod &&
            cursorKind != CXCursorKind.CXCursor_Destructor &&
            cursorKind != CXCursorKind.CXCursor_FunctionDecl)
                return CXChildVisitResult.CXChildVisit_Recurse;

            var info = new FunctionInfo
            {
                Kind = GetFunctionKind(cursorKind),
                Name = c.Spelling.CString,
                Content = GetClangSubstring(TUnit, c.Extent)
            };

            CXCursor curParent1 = c.SemanticParent;
            CXCursor curParent2 = curParent1.SemanticParent;

            if (cursorKind == CXCursorKind.CXCursor_FunctionDecl)
            {
                // A function does not belong to a class, so curParent1 should represent a
                // namespace. curParent2 should be a translation unit. Because this does not
                // belong to a class, it also doesn't belong to any brigen class.
                // Therefore we don't look up any parent class for this FunctionInfo,
                // which will cause it to be generated / preserved.
            }
            else
            {
                // A method belongs to a class, which means that curParent1 should be a class
                // and curParent2 should be a namespace. If not, this function is not compatible
                // with our detection and counts as not existing, meaning it will be generated.
                if (curParent2.Kind == CXCursorKind.CXCursor_Namespace && curParent1.Kind == CXCursorKind.CXCursor_ClassDecl)
                {
                    using CXString parentClassName = curParent1.Spelling;
                    using CXString parentNamespaceName = curParent2.Spelling;

                    info.ParentClass = DiffDetector.FindClassDecl(parentNamespaceName.CString, parentClassName.CString);
                }
            }

            string signature = ExtractClangFunctionSignature(info.Content, false);

#if DEBUG
            info.Signature = signature;
#endif

            info.SignatureIDString = FunctionDecl.ComputeSignatureIDString(signature);

            // This means that the function signature could not be extracted,
            // because the function may be ill-declared / has syntax errors.
            // In this case, we ignore this function completely.
            if (info.SignatureIDString == string.Empty)
                return CXChildVisitResult.CXChildVisit_Recurse;

            Functions.Add(info);

            return CXChildVisitResult.CXChildVisit_Recurse;
        }, default);

        SortBasedOnVisibility(Functions);

        return true;
    }
}