using System.Text;

namespace QuickResponseBao.Core.Services;

public sealed class SearchPhraseBuffer(int capacity = 64)
{
    private readonly StringBuilder _value = new(capacity);
    public int Length => _value.Length;
    public string Value => _value.ToString();
    public bool IsReady(int minimumLength) => _value.Length >= minimumLength;

    public bool AppendLetter(char letter)
    {
        if (!char.IsAsciiLetter(letter)) return false;
        EnsureCapacity(); _value.Append(letter); return true;
    }

    public bool AppendSpace()
    {
        if (_value.Length == 0 || _value[^1] == ' ') return false;
        EnsureCapacity(); _value.Append(' '); return true;
    }

    public bool Backspace()
    {
        if (_value.Length == 0) return false;
        _value.Length--; return true;
    }

    public bool Clear()
    {
        if (_value.Length == 0) return false;
        _value.Clear(); return true;
    }

    private void EnsureCapacity()
    {
        if (_value.Length < capacity) return;
        _value.Remove(0, 1);
        while (_value.Length > 0 && _value[0] == ' ') _value.Remove(0, 1);
    }
}
