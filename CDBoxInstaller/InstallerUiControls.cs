using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal static class InstallerTheme
    {
        public static readonly Color Window = Color.FromArgb(245, 247, 250);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(31, 35, 41);
        public static readonly Color Secondary = Color.FromArgb(100, 106, 115);
        public static readonly Color Weak = Color.FromArgb(143, 149, 158);
        public static readonly Color Border = Color.FromArgb(229, 232, 235);
        public static readonly Color Accent = Color.FromArgb(22, 119, 255);
        public static readonly Color AccentSoft = Color.FromArgb(235, 244, 255);
        public static readonly Color Success = Color.FromArgb(40, 145, 84);
        public static readonly Color Warning = Color.FromArgb(201, 120, 25);
        public static readonly Color Danger = Color.FromArgb(204, 62, 51);
    }

    internal sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 8;
        public Color BorderColor { get; set; } = InstallerTheme.Border;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            BackColor = InstallerTheme.Card;
            ResizeRedraw = true;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            using (GraphicsPath path = RoundedPath(ClientRectangle, Radius))
            {
                Region previous = Region;
                Region = new Region(path);
                previous?.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (GraphicsPath path = RoundedPath(bounds, Radius))
            using (var pen = new Pen(BorderColor))
                e.Graphics.DrawPath(pen, path);
        }

        internal static GraphicsPath RoundedPath(Rectangle bounds,
            int radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;
            int diameter = Math.Max(2, Math.Min(radius * 2,
                Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location,
                new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class InstallerButton : Button
    {
        private bool _primary;
        private bool _danger;

        public bool Primary
        {
            get { return _primary; }
            set { _primary = value; ApplyColors(); }
        }

        public bool Danger
        {
            get { return _danger; }
            set { _danger = value; ApplyColors(); }
        }

        public InstallerButton()
        {
            Height = 32;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 8.5F,
                FontStyle.Regular, GraphicsUnit.Point);
            Margin = new Padding(3);
            UseVisualStyleBackColor = false;
            ApplyColors();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = RoundedPanel.RoundedPath(
                ClientRectangle, 6))
            {
                Region previous = Region;
                Region = new Region(path);
                previous?.Dispose();
            }
        }

        private void ApplyColors()
        {
            if (_primary)
            {
                BackColor = InstallerTheme.Accent;
                ForeColor = Color.White;
                FlatAppearance.BorderColor = InstallerTheme.Accent;
                FlatAppearance.MouseOverBackColor = Color.FromArgb(
                    13, 101, 225);
                FlatAppearance.MouseDownBackColor = Color.FromArgb(
                    10, 87, 196);
            }
            else
            {
                BackColor = InstallerTheme.Card;
                ForeColor = _danger ? InstallerTheme.Danger
                    : InstallerTheme.Text;
                FlatAppearance.BorderColor = _danger
                    ? Color.FromArgb(242, 190, 185)
                    : InstallerTheme.Border;
                FlatAppearance.MouseOverBackColor = _danger
                    ? Color.FromArgb(255, 242, 240)
                    : Color.FromArgb(247, 249, 252);
                FlatAppearance.MouseDownBackColor = Color.FromArgb(
                    238, 242, 247);
            }
        }
    }

    internal sealed class ModernCheckBox : CheckBox
    {
        public ModernCheckBox()
        {
            Appearance = System.Windows.Forms.Appearance.Button;
            AutoSize = false;
            Size = new Size(20, 20);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            TextAlign = ContentAlignment.MiddleCenter;
            Font = new Font("Segoe UI Symbol", 9F,
                FontStyle.Regular, GraphicsUnit.Point);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            CheckedChanged += delegate { ApplyState(); };
            EnabledChanged += delegate { ApplyState(); };
            ApplyState();
        }

        private void ApplyState()
        {
            Text = Checked ? "✓" : string.Empty;
            BackColor = Checked ? InstallerTheme.Accent
                : InstallerTheme.Card;
            ForeColor = Color.White;
            FlatAppearance.BorderColor = Checked
                ? InstallerTheme.Accent : InstallerTheme.Border;
            if (!Enabled)
            {
                BackColor = Checked ? Color.FromArgb(137, 183, 245)
                    : Color.FromArgb(245, 246, 248);
                Cursor = Cursors.Default;
            }
        }
    }

    internal sealed class StatusBadge : Label
    {
        private Color _fill = Color.FromArgb(241, 243, 245);

        public StatusBadge()
        {
            AutoSize = false;
            Size = new Size(72, 22);
            TextAlign = ContentAlignment.MiddleCenter;
            Font = new Font("Microsoft YaHei UI", 8F,
                FontStyle.Regular, GraphicsUnit.Point);
            ForeColor = InstallerTheme.Secondary;
            BackColor = Color.Transparent;
        }

        public void SetState(string text, Color foreground, Color fill)
        {
            Text = text ?? string.Empty;
            ForeColor = foreground;
            _fill = fill;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            using (GraphicsPath path = RoundedPanel.RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(_fill))
                e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                ForeColor, TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ComponentTreeItemControl : UserControl
    {
        private readonly CDBoxComponentDefinition _component;
        private readonly InstallerComponentPresentation _presentation;
        private readonly Panel _header;
        private readonly Panel _indicator;
        private readonly Button _expandButton;
        private readonly Label _title;
        private readonly StatusBadge _badge;
        private readonly FlowLayoutPanel _children;
        private readonly Action<ComponentTreeItemControl> _selected;
        private bool _expanded;

        public ComponentTreeItemControl(CDBoxComponentDefinition component,
            InstallerComponentPresentation presentation,
            Action<ComponentTreeItemControl> selected,
            Action selectionChanged)
        {
            _component = component;
            _presentation = presentation;
            _selected = selected;
            BackColor = InstallerTheme.Card;
            Height = 40;
            Margin = new Padding(0, 1, 0, 1);

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = InstallerTheme.Card,
                Cursor = Cursors.Hand
            };
            _indicator = new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = Color.Transparent
            };
            _expandButton = new Button
            {
                Dock = DockStyle.Left,
                Width = 26,
                Text = "+",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = InstallerTheme.Secondary,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _expandButton.FlatAppearance.BorderSize = 0;
            CheckBox = new ModernCheckBox
            {
                Location = new Point(34, 9),
                Checked = component.Required || component.DefaultSelected,
                Enabled = component.CanChangeSelection,
                Tag = component
            };
            _badge = new StatusBadge
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(228, 8)
            };
            _title = new Label
            {
                AutoEllipsis = true,
                Location = new Point(62, 0),
                Size = new Size(160, 38),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = component.DisplayName,
                ForeColor = InstallerTheme.Text,
                Font = new Font("Microsoft YaHei UI", 9F,
                    FontStyle.Regular, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };
            _children = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                BackColor = Color.FromArgb(250, 251, 252),
                Padding = new Padding(60, 4, 4, 5),
                Visible = false
            };
            foreach (InstallerFeatureGroup group in presentation.Groups)
                _children.Controls.Add(new Label
                {
                    AutoSize = false,
                    Size = new Size(205, 24),
                    Text = group.Name,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = InstallerTheme.Secondary,
                    Font = new Font("Microsoft YaHei UI", 8.5F,
                        FontStyle.Regular, GraphicsUnit.Point),
                    Margin = new Padding(0)
                });
            Controls.Add(_children);
            Controls.Add(_header);
            _header.Controls.Add(_badge);
            _header.Controls.Add(_title);
            _header.Controls.Add(CheckBox);
            _header.Controls.Add(_expandButton);
            _header.Controls.Add(_indicator);

            _expandButton.Click += delegate { ToggleExpanded(); SelectItem(); };
            _title.Click += delegate { SelectItem(); };
            _header.Click += delegate { SelectItem(); };
            CheckBox.CheckedChanged += delegate
            {
                if (CheckBox.Enabled) selectionChanged?.Invoke();
                SelectItem();
            };
            Resize += delegate
            {
                _badge.Left = Math.Max(120, Width - _badge.Width - 10);
                _title.Width = Math.Max(60, _badge.Left - _title.Left - 6);
                foreach (Control child in _children.Controls)
                    child.Width = Math.Max(80, Width - 68);
            };
        }

        public CDBoxComponentDefinition Component => _component;
        public ModernCheckBox CheckBox { get; }

        public void SetSelected(bool selected)
        {
            _header.BackColor = selected ? InstallerTheme.AccentSoft
                : InstallerTheme.Card;
            _indicator.BackColor = selected ? InstallerTheme.Accent
                : Color.Transparent;
            _title.ForeColor = selected ? InstallerTheme.Accent
                : InstallerTheme.Text;
        }

        public void SetState(InstallerComponentState state)
        {
            switch (state)
            {
                case InstallerComponentState.Required:
                    _badge.SetState("必需", InstallerTheme.Secondary,
                        Color.FromArgb(240, 242, 245));
                    break;
                case InstallerComponentState.Installed:
                    _badge.SetState("已安装", InstallerTheme.Success,
                        Color.FromArgb(235, 248, 240));
                    break;
                case InstallerComponentState.PendingInstall:
                    _badge.SetState("将安装", InstallerTheme.Accent,
                        InstallerTheme.AccentSoft);
                    break;
                case InstallerComponentState.PendingRemove:
                    _badge.SetState("将删除", InstallerTheme.Danger,
                        Color.FromArgb(255, 240, 238));
                    break;
                case InstallerComponentState.UpdateAvailable:
                    _badge.SetState("可更新", InstallerTheme.Accent,
                        InstallerTheme.AccentSoft);
                    break;
                case InstallerComponentState.RepairRequired:
                    _badge.SetState("将修复", InstallerTheme.Warning,
                        Color.FromArgb(255, 247, 232));
                    break;
                case InstallerComponentState.Incompatible:
                    _badge.SetState("不兼容", InstallerTheme.Danger,
                        Color.FromArgb(255, 240, 238));
                    break;
                default:
                    _badge.SetState("未安装", InstallerTheme.Weak,
                        Color.FromArgb(245, 246, 248));
                    break;
            }
        }

        public string[] GroupNames()
        {
            return _presentation.Groups.Select(x => x.Name).ToArray();
        }

        private void ToggleExpanded()
        {
            _expanded = !_expanded;
            _expandButton.Text = _expanded ? "−" : "+";
            _children.Visible = _expanded;
            _children.Height = _expanded
                ? _presentation.Groups.Length * 24 + 9 : 0;
            Height = 40 + _children.Height;
        }

        private void SelectItem()
        {
            _selected?.Invoke(this);
        }
    }
}
