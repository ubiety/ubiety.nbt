using System.Globalization;
using System.Text;
using Ubiety.Nbt.IO;

namespace Ubiety.Nbt.Snbt;

/// <summary>
/// Parses SNBT (stringified NBT), the text syntax used by Minecraft commands.
/// </summary>
/// <remarks>
/// Unsuffixed integers are ints, unsuffixed decimals are doubles, <c>true</c>/<c>false</c> are bytes, and any
/// other unquoted token is a string. Lists must be homogeneous.
/// </remarks>
internal sealed class SnbtParser
{
    private readonly string _text;
    private int _position;

    private SnbtParser(string text)
    {
        _text = text;
    }

    private bool AtEnd => _position >= _text.Length;

    public static NbtTag Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new SnbtParser(text);
        parser.SkipWhitespace();
        var tag = parser.ReadValue(0);
        parser.SkipWhitespace();
        if (!parser.AtEnd)
        {
            throw parser.Error("Unexpected trailing characters");
        }

        return tag;
    }

    public static bool IsUnquotedChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '+';

    private NbtTag ReadValue(int depth)
    {
        if (AtEnd)
        {
            throw Error("Expected a value");
        }

        return _text[_position] switch
        {
            '{' => ReadCompound(depth),
            '[' => ReadListOrArray(depth),
            '"' or '\'' => new NbtString(ReadQuotedString()),
            _ => ReadLiteral(),
        };
    }

    private NbtCompound ReadCompound(int depth)
    {
        CheckDepth(depth);
        Expect('{');
        var compound = new NbtCompound();
        SkipWhitespace();
        if (TryConsume('}'))
        {
            return compound;
        }

        while (true)
        {
            SkipWhitespace();
            var key = ReadKey();
            SkipWhitespace();
            Expect(':');
            SkipWhitespace();
            compound[key] = ReadValue(depth + 1);
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return compound;
            }

            Expect(',');
        }
    }

    private NbtTag ReadListOrArray(int depth)
    {
        CheckDepth(depth);
        Expect('[');
        if (_position + 1 < _text.Length && _text[_position] is 'B' or 'I' or 'L' && _text[_position + 1] == ';')
        {
            var kind = _text[_position];
            _position += 2;
            return ReadArray(kind);
        }

        var list = new NbtList();
        SkipWhitespace();
        if (TryConsume(']'))
        {
            return list;
        }

        while (true)
        {
            SkipWhitespace();
            var start = _position;
            var element = ReadValue(depth + 1);
            if (list.Count > 0 && element.TagType != list.ElementType)
            {
                throw Error($"Cannot add {element.TagType} to a list of {list.ElementType}", start);
            }

            list.Add(element);
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return list;
            }

            Expect(',');
        }
    }

    private NbtTag ReadArray(char kind)
    {
        var (min, max, typeName) = kind switch
        {
            'B' => ((long)sbyte.MinValue, (long)sbyte.MaxValue, "byte"),
            'I' => (int.MinValue, int.MaxValue, "int"),
            _ => (long.MinValue, long.MaxValue, "long"),
        };

        var values = new List<long>();
        SkipWhitespace();
        if (!TryConsume(']'))
        {
            while (true)
            {
                SkipWhitespace();
                var start = _position;
                long value = ReadLiteral() switch
                {
                    NbtByte b => unchecked((sbyte)b.Value),
                    NbtShort s => s.Value,
                    NbtInt i => i.Value,
                    NbtLong l => l.Value,
                    var other => throw Error($"Expected an integer in {typeName} array but found {other.TagType}", start),
                };

                if (value < min || value > max)
                {
                    throw Error($"Value {value} is out of range for a {typeName} array", start);
                }

                values.Add(value);
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    break;
                }

                Expect(',');
            }
        }

        return kind switch
        {
            'B' => new NbtByteArray(values.Select(v => unchecked((byte)v)).ToArray()),
            'I' => new NbtIntArray(values.Select(v => (int)v).ToArray()),
            _ => new NbtLongArray([.. values]),
        };
    }

    private string ReadKey()
    {
        if (!AtEnd && _text[_position] is '"' or '\'')
        {
            return ReadQuotedString();
        }

        var key = ReadUnquoted();
        return key.Length > 0 ? key : throw Error("Expected a key");
    }

    private string ReadQuotedString()
    {
        var quote = _text[_position++];
        var builder = new StringBuilder();
        while (true)
        {
            if (AtEnd)
            {
                throw Error("Unterminated string");
            }

            var c = _text[_position++];
            if (c == quote)
            {
                return builder.ToString();
            }

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (AtEnd)
            {
                throw Error("Unterminated string");
            }

            var escape = _text[_position++];
            switch (escape)
            {
                case '\\' or '"' or '\'':
                    builder.Append(escape);
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 's':
                    builder.Append(' ');
                    break;
                case 'x':
                    builder.Append((char)ReadHex(2));
                    break;
                case 'u':
                    builder.Append((char)ReadHex(4));
                    break;
                case 'U':
                    var start = _position;
                    var codePoint = ReadHex(8);
                    if (codePoint is < 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF))
                    {
                        throw Error("Invalid Unicode code point", start);
                    }

                    builder.Append(char.ConvertFromUtf32(codePoint));
                    break;
                default:
                    throw Error($"Invalid escape sequence '\\{escape}'", _position - 2);
            }
        }
    }

    private int ReadHex(int digits)
    {
        if (_position + digits > _text.Length ||
            !int.TryParse(_text.AsSpan(_position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            throw Error($"Expected {digits} hex digits");
        }

        _position += digits;
        return value;
    }

    private string ReadUnquoted()
    {
        var start = _position;
        while (!AtEnd && IsUnquotedChar(_text[_position]))
        {
            _position++;
        }

        return _text[start.._position];
    }

    private NbtTag ReadLiteral()
    {
        var token = ReadUnquoted();
        return token.Length > 0
            ? ParseLiteral(token)
            : throw Error(AtEnd ? "Expected a value" : $"Unexpected character '{_text[_position]}'");
    }

    private static NbtTag ParseLiteral(string token)
    {
        if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new NbtByte(true);
        }

        if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new NbtByte(false);
        }

        var invariant = CultureInfo.InvariantCulture;
        var body = token.AsSpan(0, token.Length - 1);
        switch (char.ToLowerInvariant(token[^1]))
        {
            case 'b' when IsInteger(body) && sbyte.TryParse(body, NumberStyles.AllowLeadingSign, invariant, out var b):
                return new NbtByte(unchecked((byte)b));
            case 's' when IsInteger(body) && short.TryParse(body, NumberStyles.AllowLeadingSign, invariant, out var s):
                return new NbtShort(s);
            case 'l' when IsInteger(body) && long.TryParse(body, NumberStyles.AllowLeadingSign, invariant, out var l):
                return new NbtLong(l);
            case 'f' when IsDecimal(body) && float.TryParse(body, NumberStyles.Float, invariant, out var f):
                return new NbtFloat(f);
            case 'd' when IsDecimal(body) && double.TryParse(body, NumberStyles.Float, invariant, out var d):
                return new NbtDouble(d);
        }

        if (IsInteger(token) && int.TryParse(token, NumberStyles.AllowLeadingSign, invariant, out var i))
        {
            return new NbtInt(i);
        }

        // Like Minecraft, an unsuffixed number must contain a '.' to be a double; out-of-range integers are strings.
        if (token.Contains('.') && IsDecimal(token) && double.TryParse(token, NumberStyles.Float, invariant, out var number))
        {
            return new NbtDouble(number);
        }

        return new NbtString(token);
    }

    private static bool IsInteger(ReadOnlySpan<char> s)
    {
        var i = s.Length > 0 && s[0] is '+' or '-' ? 1 : 0;
        if (i == s.Length)
        {
            return false;
        }

        for (; i < s.Length; i++)
        {
            if (!char.IsAsciiDigit(s[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Matches [-+]?(?:[0-9]+[.]?|[0-9]*[.][0-9]+)(?:[eE][-+]?[0-9]+)?
    private static bool IsDecimal(ReadOnlySpan<char> s)
    {
        var i = s.Length > 0 && s[0] is '+' or '-' ? 1 : 0;
        var digits = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            i++;
            digits++;
        }

        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
                digits++;
            }
        }

        if (digits == 0)
        {
            return false;
        }

        if (i < s.Length && s[i] is 'e' or 'E')
        {
            i++;
            if (i < s.Length && s[i] is '+' or '-')
            {
                i++;
            }

            var exponentDigits = 0;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
                exponentDigits++;
            }

            if (exponentDigits == 0)
            {
                return false;
            }
        }

        return i == s.Length;
    }

    private void CheckDepth(int depth)
    {
        if (depth >= NbtBinaryReader.DefaultMaxDepth)
        {
            throw Error($"SNBT is nested deeper than the maximum depth of {NbtBinaryReader.DefaultMaxDepth}");
        }
    }

    private void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    private bool TryConsume(char c)
    {
        if (AtEnd || _text[_position] != c)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void Expect(char c)
    {
        if (!TryConsume(c))
        {
            throw Error(AtEnd ? $"Expected '{c}' but reached the end" : $"Expected '{c}' but found '{_text[_position]}'");
        }
    }

    private NbtFormatException Error(string message, int? position = null) =>
        new($"{message} at position {position ?? _position}.");
}
