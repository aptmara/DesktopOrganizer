using System.Windows.Forms;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// プロファイル名入力用のシンプルなダイアログ
/// </summary>
public class SaveProfileDialog : Form
{
    private TextBox _textBox;
    private Button _okButton;
    private Button _cancelButton;

    public string ProfileName => _textBox.Text.Trim();

    public SaveProfileDialog()
    {
        Text = "プロファイルを保存";
        Width = 350;
        Height = 150;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(30, 30, 36);
        ForeColor = System.Drawing.Color.White;

        var label = new Label
        {
            Text = "プロファイル名:",
            Left = 20,
            Top = 20,
            Width = 100,
            ForeColor = System.Drawing.Color.White
        };
        Controls.Add(label);

        _textBox = new TextBox
        {
            Left = 20,
            Top = 45,
            Width = 290,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 55),
            ForeColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_textBox);

        _okButton = new Button
        {
            Text = "OK",
            Left = 150,
            Top = 75,
            Width = 75,
            DialogResult = DialogResult.OK,
            BackColor = System.Drawing.Color.FromArgb(100, 108, 255),
            FlatStyle = FlatStyle.Flat
        };
        _okButton.FlatAppearance.BorderSize = 0;
        Controls.Add(_okButton);

        _cancelButton = new Button
        {
            Text = "キャンセル",
            Left = 235,
            Top = 75,
            Width = 75,
            DialogResult = DialogResult.Cancel,
            BackColor = System.Drawing.Color.FromArgb(60, 60, 70),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.FlatAppearance.BorderSize = 0;
        Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }
}
