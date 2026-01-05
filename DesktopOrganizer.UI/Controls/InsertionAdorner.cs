using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;

namespace DesktopOrganizer.UI.Controls;

public class InsertionAdorner : Adorner
{
    private Point _startPoint;
    private Point _endPoint;
    private readonly Pen _pen;

    public InsertionAdorner(UIElement adornedElement, Point start, Point end)
        : base(adornedElement)
    {
        _startPoint = start;
        _endPoint = end;
        // Azure / DodgerBlue color for visibility
        _pen = new Pen(new SolidColorBrush(Color.FromRgb(30, 144, 255)), 3);
        _pen.Freeze();
        IsHitTestVisible = false;
    }

    public void UpdatePosition(Point start, Point end)
    {
        _startPoint = start;
        _endPoint = end;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawLine(_pen, _startPoint, _endPoint);
    }
}
