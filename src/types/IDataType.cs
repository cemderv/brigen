namespace brigen.types;

public interface IDataType
{
    string Name { get; }
    bool IsClass { get; }
    bool IsArray { get; }
    bool IsStruct { get; }
    bool IsDelegate { get; }
    bool IsEnum { get; }
    bool IsUserDefined { get; }

    IDataType VerifyType(Module module);
}