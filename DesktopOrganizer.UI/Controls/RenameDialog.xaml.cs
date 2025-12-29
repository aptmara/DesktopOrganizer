using System.Windows;

namespace DesktopOrganizer.UI.Controls;

public partial class RenameDialog : Window
{
    public string ResultName { get; private set; } = string.Empty;

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ResultName = NameTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
