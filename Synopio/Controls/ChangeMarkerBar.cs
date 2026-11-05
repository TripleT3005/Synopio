using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using Synopio.Engine.Comparison;
using Synopio.ViewModels;

namespace Synopio.Controls;

public sealed class ChangeMarkerBar : FrameworkElement
{
    // Constants
    private const double MinimumMarkerHeight = 2;

    // Fields
    private static readonly Brush DeletedBrush = CreateBrush(0xE8, 0x11, 0x23);
    private static readonly Brush InsertedBrush = CreateBrush(0x2F, 0xB3, 0x6B);

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(IEnumerable),
        typeof(ChangeMarkerBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsChanged));

    // Properties
    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    // Rendering
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (Items == null)
        {
            return;
        }

        var rows = Items.OfType<SynopsisRowViewModel>().ToList();
        if (rows.Count == 0 || ActualHeight <= 0)
        {
            return;
        }

        var rowHeight = ActualHeight / rows.Count;
        var markerHeight = Math.Max(MinimumMarkerHeight, rowHeight);
        var halfWidth = ActualWidth / 2;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index].Row;
            if (!row.HasChanges)
            {
                continue;
            }

            var top = Math.Min(index * rowHeight, ActualHeight - markerHeight);
            var hasDeletion = row.Left.Any(segment => segment.Kind == SegmentKind.Deleted);
            var hasInsertion = row.Right.Any(segment => segment.Kind == SegmentKind.Inserted);

            if (hasDeletion && hasInsertion)
            {
                drawingContext.DrawRectangle(DeletedBrush, null, new Rect(0, top, halfWidth, markerHeight));
                drawingContext.DrawRectangle(InsertedBrush, null, new Rect(halfWidth, top, halfWidth, markerHeight));
            }
            else
            {
                drawingContext.DrawRectangle(hasDeletion ? DeletedBrush : InsertedBrush, null, new Rect(0, top, ActualWidth, markerHeight));
            }
        }
    }

    // Change tracking
    private static void OnItemsChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        var bar = (ChangeMarkerBar)element;
        if (args.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= bar.OnCollectionChanged;
        }

        if (args.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += bar.OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
