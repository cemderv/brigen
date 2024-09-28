namespace brigen.decl;

public enum AttributeKind
{
    Undefined = 0,
    AbstractImpl,
    OperatorAdd,
    OperatorSubtract,
    OperatorMultiply,
    OperatorDivide
}

public sealed class AttributeDecl(string name, CodeRange range) : Decl(name, range)
{
    public AttributeKind Kind { get; private set; }

    protected override void OnVerify(Module module) =>
      Kind = Name switch
      {
          "abstract_impl" => AttributeKind.AbstractImpl,
          "op_add" => AttributeKind.OperatorAdd,
          "op_subtract" => AttributeKind.OperatorSubtract,
          "op_multiply" => AttributeKind.OperatorMultiply,
          "op_divide" => AttributeKind.OperatorDivide,
          _ => throw new CompileError($"Invalid attribute '{Name}' specified", Range)
      };
}