using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Synopio.Engine.Text;

namespace Synopio.Engine.Comparison;

internal static partial class Tokenizer
{
    // Methods
    public static List<Token> Tokenize(IReadOnlyList<Paragraph> paragraphs, ComparisonOptions options)
    {
        var tokens = new List<Token>();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            var text = paragraphs[index].Text;
            var previousEnd = 0;

            foreach (Match match in TokenPattern().Matches(text))
            {
                var hasLeadingSpace = match.Index > previousEnd;
                var isPunctuation = !char.IsLetterOrDigit(match.Value[0]);
                tokens.Add(new Token(match.Value, CreateKey(match.Value, options), hasLeadingSpace, index, isPunctuation));
                previousEnd = match.Index + match.Length;
            }
        }

        return tokens;
    }

    internal static string CreateKey(string text, ComparisonOptions options)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormKC))
        {
            var mapped = character switch
            {
                '„' or '“' or '”' or '«' or '»' or '″' => '"',
                '‚' or '‘' or '’' or '‹' or '›' or '′' or '`' or '´' => '\'',
                '‐' or '‑' or '‒' or '–' or '—' or '−' => '-',
                _ => character,
            };

            if (mapped == '-' && options.IgnoreHyphens && text.Length > 1)
            {
                continue;
            }

            builder.Append(mapped);
        }

        var key = builder.ToString();
        if (key == ",,")
        {
            key = "\"";
        }
        else if (key.Length >= 3 && key.All(character => character is '.' or '…' or '·' or '_'))
        {
            key = "…";
        }

        return options.IgnoreCase ? key.ToLower(CultureInfo.InvariantCulture) : key;
    }

    [GeneratedRegex(@"[.…·_]{3,}|[\p{L}\p{N}\p{M}]+(?:[-‐‑'’][\p{L}\p{N}\p{M}]+)*-?|,,|[^\s\p{L}\p{N}\p{M}]")]
    private static partial Regex TokenPattern();
}
