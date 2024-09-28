namespace brigen.types;

public sealed class PrimitiveType(string name) : IDataType
{
    public static readonly PrimitiveType Undefined = new("undefined");
    public static readonly PrimitiveType Byte = new("byte");
    public static readonly PrimitiveType Int = new("int");
    public static readonly PrimitiveType Short = new("short");
    public static readonly PrimitiveType Long = new("long");
    public static readonly PrimitiveType Bool = new("bool");
    public static readonly PrimitiveType Float = new("float");
    public static readonly PrimitiveType Double = new("double");
    public static readonly PrimitiveType String = new("string");
    public static readonly PrimitiveType Handle = new("handle");
    public static readonly PrimitiveType Void = new("void");

    private static readonly Dictionary<string, PrimitiveType> _primitiveTypes = new()
  {
    { Byte.Name, Byte },
    { Int.Name, Int },
    { Short.Name, Short },
    { Long.Name, Long },
    { Bool.Name, Bool },
    { Float.Name, Float },
    { Double.Name, Double },
    { String.Name, String },
    { Handle.Name, Handle },
    { Void.Name, Void }
  };

    public IDataType VerifyType(Module module) => this;

    public string Name { get; } = name;

    public bool IsClass => false;
    public bool IsArray => false;
    public bool IsStruct => false;
    public bool IsDelegate => false;
    public bool IsEnum => false;
    public bool IsUserDefined => false;

    public static PrimitiveType? Get(string name)
      => _primitiveTypes.TryGetValue(name, out PrimitiveType? type) ? type : null;

    public override string ToString() => $"PrimitiveType '{Name}'";
}