using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace NetworkProfiles.App;

/// <summary>Paints the tray menu in the app's palette (WinForms menus are light otherwise).</summary>
internal sealed class DarkMenuRenderer() : Forms.ToolStripProfessionalRenderer(new Palette())
{
    private static readonly Color Text = Color.FromArgb(0xf3, 0xf3, 0xf4);
    private static readonly Color DisabledText = Color.FromArgb(0x80, 0x80, 0x88);
    private static readonly Color Red = Color.FromArgb(0xef, 0x4b, 0x55);

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : DisabledText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Text;
        base.OnRenderArrow(e);
    }

    /// <summary>A red tick instead of the default dark glyph on a highlighted square.</summary>
    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(e.Item.Enabled ? Red : DisabledText, Math.Max(2f, r.Height / 8f))
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round,
        };
        g.DrawLines(pen, [
            new PointF(r.Left + r.Width * 0.22f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
            new PointF(r.Left + r.Width * 0.78f, r.Top + r.Height * 0.30f),
        ]);
    }

    private sealed class Palette : Forms.ProfessionalColorTable
    {
        private static readonly Color Card = Color.FromArgb(0x15, 0x15, 0x17);
        private static readonly Color Hover = Color.FromArgb(0x25, 0x25, 0x29);
        private static readonly Color Border = Color.FromArgb(0x2a, 0x2a, 0x2f);

        public Palette() => UseSystemColors = false;

        public override Color ToolStripDropDownBackground => Card;
        public override Color ImageMarginGradientBegin => Card;
        public override Color ImageMarginGradientMiddle => Card;
        public override Color ImageMarginGradientEnd => Card;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Card;
        public override Color CheckBackground => Card;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
    }
}
