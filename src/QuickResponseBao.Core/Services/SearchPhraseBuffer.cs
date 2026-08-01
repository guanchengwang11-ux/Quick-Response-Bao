using System.Text;

namespace QuickResponseBao.Core.Services;

public sealed class SearchPhraseBuffer(int capacity = 64)
{
    private readonly StringBuilder _value = new(capacity);
    private readonly StringBuilder _raw = new(capacity);
    public int Length => _value.Length;
    public string Value => _value.ToString();
    public int RawTypedCharacterCount => _raw.Length;
    public string RawTypedText => _raw.ToString();
    public bool IsReady(int minimumLength) => _value.Length >= minimumLength;

    public bool AppendLetter(char letter)
    {
        if (!char.IsAsciiLetter(letter)) return false;
        EnsureCapacity(); _raw.Append(letter); RebuildNormalized(); return true;
    }

    public bool AppendSpace()
    {
        if (_raw.Length == 0) return false;
        EnsureCapacity(); _raw.Append(' '); RebuildNormalized(); return true;
    }

    public bool Backspace()
    {
        if (_raw.Length == 0) return false;
        _raw.Length--; RebuildNormalized(); return true;
    }

    public bool Clear()
    {
        if (_raw.Length == 0) return false;
        _raw.Clear(); _value.Clear(); return true;
    }

    private void EnsureCapacity()
    {
        if (_raw.Length < capacity) return;
        _raw.Clear(); _value.Clear();
    }

    private void RebuildNormalized()
    {
        _value.Clear();
        foreach (var character in _raw.ToString())
        {
            if (character == ' ')
            {
                if (_value.Length > 0 && _value[^1] != ' ') _value.Append(character);
            }
            else _value.Append(character);
        }
    }
}
