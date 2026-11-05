using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Synopio.Engine.Comparison;

namespace Synopio.Controls;

public static class SegmentText
{
    // Fields
    private static readonly Brush DeletedBackground = CreateBrush(0x40, 0xE8, 0x11, 0x23);
    private static readonly Brush InsertedBackground = CreateBrush(0x40, 0x2F, 0xB3, 0x6B);

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
        "Segments",
        typeof(IReadOnlyList<Segment>),
        typeof(SegmentText),
        new PropertyMetadata(null, OnSegmentsChanged));

    // Methods
    public static IReadOnlyList<Segment>? GetSegments(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (IReadOnlyList<Segment>?)element.GetValue(SegmentsProperty);
    }

    public static void SetSegments(DependencyObject element, IReadOnlyList<Segment>? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SegmentsProperty, value);
    }

    private static void OnSegmentsChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();
        if (args.NewValue is not IReadOnlyList<Segment> segments)
        {
            return;
        }

        foreach (var segment in segments)
        {
            textBlock.Inlines.Add(CreateInline(segment));
        }
    }

    private static Inline CreateInline(Segment segment)
    {
        switch (segment.Kind)
        {
            case SegmentKind.ParagraphBreak:
                return new Span(new LineBreak()) { Inlines = { new LineBreak() } };
            case SegmentKind.Deleted:
                return new Run(segment.Text) { Background = DeletedBackground, TextDecorations = TextDecorations.Strikethrough };
            case SegmentKind.Inserted:
                return new Run(segment.Text) { Background = InsertedBackground, TextDecorations = TextDecorations.Underline };
            default:
                return new Run(segment.Text);
        }
    }

    private static SolidColorBrush CreateBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }
}
