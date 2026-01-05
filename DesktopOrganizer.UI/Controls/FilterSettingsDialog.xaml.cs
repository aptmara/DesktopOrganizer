using System.Windows;

namespace DesktopOrganizer.UI.Controls;

public partial class FilterSettingsDialog : Window
{
    public string ResultPattern { get; private set; } = string.Empty;

    public FilterSettingsDialog(string currentPattern)
    {
        InitializeComponent();
        PatternTextBox.Text = currentPattern;
        PatternTextBox.Focus();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ResultPattern = PatternTextBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
