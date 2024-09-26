using System.Diagnostics;

namespace brigen.types;

public sealed class ArrayType : IDataType
{
    private static readonly Dictionary<IDataType, ArrayType> _map = new();

    private ArrayType(IDataType elementType)
    {
        ElementType = elementType;
    }

    public IDataType ElementType { get; }

    public IDataType VerifyType(Module module)
    {
        IDataType res = ElementType.VerifyType(module);

        Debug.Assert(res is not UnresolvedType);

        return Get(res);
    }

    public string Name => $"{ElementType.Name} array";

    public bool IsClass => false;

    public bool IsArray => true;

    public bool IsStruct => false;

    public bool IsDelegate => false;

    public bool IsEnum => false;

    public bool IsUserDefined => false;

    public static ArrayType Get(IDataType elementType)
    {
        if (!_map.TryGetValue(elementType, out ArrayType? result))
        {
            result = new ArrayType(elementType);
            _map.Add(elementType, result);
        }

        return result;
    }

    public override string ToString() => $"ArrayType '{Name}'";
}