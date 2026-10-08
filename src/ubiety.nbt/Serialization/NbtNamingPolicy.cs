using System.Text;

namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Common naming policies for <see cref="NbtSerializerOptions.PropertyNamingPolicy"/>.
/// </summary>
public static class NbtNamingPolicy
{
    /// <summary>Gets a policy that converts <c>PascalCase</c> to <c>camelCase</c>, e.g. <c>UUIDMost</c> to <c>uuidMost</c>.</summary>
    public static Func<string, string> CamelCase { get; } = ToCamelCase;

    /// <summary>
    /// Gets a policy that converts <c>PascalCase</c> to <c>snake_case</c>, e.g. <c>BlockEntities</c> to
    /// <c>block_entities</c>. Newer Minecraft data uses this style.
    /// </summary>
    public static Func<string, string> SnakeCase { get; } = ToSnakeCase;

    private static string ToCamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        // Lower the leading run of capitals, except the last one if it starts the next word ("UUIDMost" -> "uuidMost").
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length && char.IsUpper(chars[i]); i++)
        {
            if (i > 0 && i + 1 < chars.Length && char.IsLower(chars[i + 1]))
            {
                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var startsWord = i > 0 &&
                    (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) ||
                     (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (startsWord && name[i - 1] != '_')
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
