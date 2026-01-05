using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopOrganizer.UI.Controls;

public partial class DragGhostWindow : Window
{
    public DragGhostWindow(Visual visual, double width, double height)
    {
        InitializeComponent();

        // Add padding for shadow (Margin="20" in XAML -> 20*2 = 40)
        this.Width = width + 40;
        this.Height = height + 40;

        var brush = new VisualBrush(visual);
        GhostVisual.Fill = brush;
    }
}
