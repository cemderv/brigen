using brigen.decl;

namespace brigen.diff;

internal enum FunctionKind
{
    Ctor = 1,
    Dtor,
    Method,
    FreeFunction
}

internal enum Visibility
{
    Public = 1,
    Protected,
    Private
}

internal interface IHasVisibility
{
    Visibility Visibility { get; }
}

internal class FunctionInfo : IHasVisibility
{
    public FunctionKind Kind { get; set; } = FunctionKind.Ctor;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
#if DEBUG
    public string Signature { get; set; } = string.Empty;
#endif
    public string SignatureIDString { get; set; } = string.Empty;
    public ClassDecl? ParentClass { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Public;

#if DEBUG
    public override string ToString() => $"{Visibility} {Kind} {Name}";
#endif
}

internal class StructInfo : IHasVisibility
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Visibility Visibility { get; set; } = Visibility.Public;

#if DEBUG
    public override string ToString() => $"{Visibility} struct {Name}";
#endif
}

internal class FieldInfo : IHasVisibility
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Visibility Visibility { get; set; } = Visibility.Public;

#if DEBUG
    public override string ToString() => $"{Visibility}";
#endif
}

internal class EnumInfo : IHasVisibility
{
    public string Content { get; set; } = string.Empty;
    public Visibility Visibility { get; set; } = Visibility.Public;

#if DEBUG
    public override string ToString() => Content;
#endif
}

internal class ClassInfo
{
    public string Namespace { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<EnumInfo> Enums { get; set; } = new();
    public List<StructInfo> Structs { get; set; } = new();
    public List<FieldInfo> Fields { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public ClassDecl? RepresentedClassDecl { get; set; }

#if DEBUG
    public override string ToString() =>
      !string.IsNullOrEmpty(Namespace) ? $"class {Namespace}::{Name}" : $"class {Name}";
#endif
}