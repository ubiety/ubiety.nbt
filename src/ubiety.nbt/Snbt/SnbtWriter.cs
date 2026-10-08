using System.Globalization;
using System.Text;
using Ubiety.Nbt.IO;

namespace Ubiety.Nbt.Snbt;

/// <summary>
/// Formats tags as SNBT (stringified NBT), the text syntax used by Minecraft commands.
/// </summary>
internal static class SnbtWriter
{
    private const int IndentSize = 4;

    public static string Write(NbtTag tag, bool indented)
    {
        var builder = new StringBuilder();
        WriteTag(builder, tag, indented, 0);
        return builder.ToString();
    }

    private static void WriteTag(StringBuilder builder, NbtTag tag, bool indented, int depth)
    {
        var invariant = CultureInfo.InvariantCulture;
        switch (tag)
        {
            case NbtByte t:
                builder.Append(invariant, $"{unchecked((sbyte)t.Value)}b");
                break;
            case NbtShort t:
                builder.Append(invariant, $"{t.Value}s");
                break;
            case NbtInt t:
                builder.Append(invariant, $"{t.Value}");
                break;
            case NbtLong t:
                builder.Append(invariant, $"{t.Value}L");
                break;
            case NbtFloat t:
                builder.Append(invariant, $"{t.Value}f");
                break;
            case NbtDouble t:
                builder.Append(invariant, $"{t.Value}d");
                break;
            case NbtString t:
                WriteQuoted(builder, t.Value);
                break;
            case NbtByteArray t:
                WriteArray(builder, 'B', t.Value.Select(v => $"{unchecked((sbyte)v)}b"), indented);
                break;
            case NbtIntArray t:
                WriteArray(builder, 'I', t.Value.Select(v => v.ToString(invariant)), indented);
                break;
            case NbtLongArray t:
                WriteArray(builder, 'L', t.Value.Select(v => v.ToString(invariant) + "L"), indented);
                break;
            case NbtList t:
                CheckDepth(depth);
                WriteList(builder, t, indented, depth);
                break;
            case NbtCompound t:
                CheckDepth(depth);
                WriteCompound(builder, t, indented, depth);
                break;
            default:
                throw new NbtFormatException($"Unsupported tag type {tag.GetType()}.");
        }
    }

    private static void CheckDepth(int depth)
    {
        if (depth >= NbtBinaryReader.DefaultMaxDepth)
        {
            throw new NbtFormatException($"The tag tree is nested deeper than the maximum depth of {NbtBinaryReader.DefaultMaxDepth}. Does it contain a cycle?");
        }
    }

    private static void WriteCompound(StringBuilder builder, NbtCompound compound, bool indented, int depth)
    {
        if (compound.Count == 0)
        {
            builder.Append("{}");
            return;
        }

        builder.Append('{');
        var first = true;
        foreach (var (name, tag) in compound)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            if (indented)
            {
                NewLine(builder, depth + 1);
            }

            WriteKey(builder, name);
            builder.Append(indented ? ": " : ":");
            WriteTag(builder, tag, indented, depth + 1);
        }

        if (indented)
        {
            NewLine(builder, depth);
        }

        builder.Append('}');
    }

    private static void WriteList(StringBuilder builder, NbtList list, bool indented, int depth)
    {
        if (list.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        // Lists of scalars stay on one line even when indented; lists of containers get one element per line.
        var multiline = indented && list.ElementType is NbtTagType.Compound or NbtTagType.List
            or NbtTagType.ByteArray or NbtTagType.IntArray or NbtTagType.LongArray;

        builder.Append('[');
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(indented && !multiline ? ", " : ",");
            }

            if (multiline)
            {
                NewLine(builder, depth + 1);
            }

            WriteTag(builder, list[i], indented, depth + 1);
        }

        if (multiline)
        {
            NewLine(builder, depth);
        }

        builder.Append(']');
    }

    private static void WriteArray(StringBuilder builder, char prefix, IEnumerable<string> values, bool indented)
    {
        builder.Append('[').Append(prefix).Append(';');
        var separator = indented ? ", " : ",";
        var first = true;
        foreach (var value in values)
        {
            builder.Append(first ? (indented ? " " : string.Empty) : separator).Append(value);
            first = false;
        }

        builder.Append(']');
    }

    private static void WriteKey(StringBuilder builder, string name)
    {
        if (name.Length > 0 && name.All(SnbtParser.IsUnquotedChar))
        {
            builder.Append(name);
        }
        else
        {
            WriteQuoted(builder, name);
        }
    }

    private static void WriteQuoted(StringBuilder builder, string value)
    {
        var quote = value.Contains('"') && !value.Contains('\'') ? '\'' : '"';
        builder.Append(quote);
        foreach (var c in value)
        {
            if (c == '\\' || c == quote)
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        builder.Append(quote);
    }

    private static void NewLine(StringBuilder builder, int depth) => builder.Append('\n').Append(' ', depth * IndentSize);
}
