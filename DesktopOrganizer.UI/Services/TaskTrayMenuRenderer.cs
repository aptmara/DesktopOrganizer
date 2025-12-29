using System.Drawing;
using System.Windows.Forms;

namespace DesktopOrganizer.UI.Services;

public class TaskTrayMenuRenderer : ToolStripProfessionalRenderer
{
    public TaskTrayMenuRenderer() : base(new DarkColorTable())
    {
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        // Background
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 36); // #1E1E24

        // Border
        public override Color MenuBorder => Color.FromArgb(51, 51, 60); // #33333C

        // Text (Handled in Renderer mostly, but Table covers some)

        // Selection
        public override Color MenuItemSelected => Color.FromArgb(60, 60, 70);
        public override Color MenuItemBorder => Color.Transparent;

        // Separator
        public override Color SeparatorDark => Color.FromArgb(80, 80, 80);
        public override Color SeparatorLight => Color.FromArgb(30, 30, 36);

        // Image Margin
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 36);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 36);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 36);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.FromArgb(238, 238, 238); // #EEEEEE
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.White;
        base.OnRenderArrow(e);
    }
}
