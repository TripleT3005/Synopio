namespace Synopio.Engine.Comparison;

internal sealed record Token(string Text, string Key, bool HasLeadingSpace, int ParagraphIndex, bool IsPunctuation);
