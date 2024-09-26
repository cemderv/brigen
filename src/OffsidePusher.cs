namespace brigen;

internal readonly struct OffsidePusher : IDisposable
{
    private readonly Stack<int> _offsideStack;

    public OffsidePusher(Stack<int> offsideStack, int column)
    {
        _offsideStack = offsideStack;
        _offsideStack.Push(column);
    }

    public void Dispose() => _offsideStack.Pop();
}