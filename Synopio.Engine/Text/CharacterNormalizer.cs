using System.Text;

namespace Synopio.Engine.Text;

internal static class CharacterNormalizer
{
    // Methods
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case 'ﬀ':
                    builder.Append("ff");
                    break;
                case 'ﬁ':
                    builder.Append("fi");
                    break;
                case 'ﬂ':
                    builder.Append("fl");
                    break;
                case 'ﬃ':
                    builder.Append("ffi");
                    break;
                case 'ﬄ':
                    builder.Append("ffl");
                    break;
                case 'ﬅ':
                case 'ﬆ':
                    builder.Append("st");
                    break;
                case ' ':
                case ' ':
                case ' ':
                case '\t':
                    builder.Append(' ');
                    break;
                case >= '' and <= '':
                    builder.Append('•');
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    public static string RemoveSoftHyphens(string text) => text.Replace("\u00AD", string.Empty, StringComparison.Ordinal);
}
