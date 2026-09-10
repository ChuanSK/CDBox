using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CDBox.RealEstate.Models;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.UI
{
    internal static class ParcelBoundaryCadDialogs
    {
        private static readonly string[] LineCategories = { "围墙", "墙壁",
            "门墩", "道路", "田埂", "沟渠", "铁丝网", "界址线", "其他" };
        private static readonly string[] LinePositions = { "内", "中", "外" };

        public static bool TryGetStartingPoint(out string prefix,
            out int number)
        {
            prefix = "J";
            number = 1;
            var dialog = new BoundaryFloatingDialog("设置起点界址点号",
                "已在图纸中选定起点，点号必须以数字结尾。", 390);
            TextBox input = dialog.AddText("起点界址点号", "J1",
                "例如 J1");
            dialog.AcceptText = "下一步";
            dialog.Validate = delegate
            {
                Match match = Regex.Match((input.Text ?? string.Empty).Trim(),
                    @"^(.*?)(\d+)$");
                if (!match.Success || string.IsNullOrWhiteSpace(
                    match.Groups[1].Value))
                    return "请输入以数字结尾的界址点号，例如 J1。";
                return string.Empty;
            };
            if (!dialog.ShowForAutoCad()) return false;
            Match result = Regex.Match(input.Text.Trim(), @"^(.*?)(\d+)$");
            prefix = result.Groups[1].Value;
            return int.TryParse(result.Groups[2].Value,
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out number);
        }

        public static bool TryGetOwnerName(string current,
            out string ownerName)
        {
            ownerName = (current ?? string.Empty).Trim();
            var dialog = new BoundaryFloatingDialog("填写宗地权利人",
                "一条权属线对应一份独立宗地数据，并以权利人姓名作为宗地名。",
                430);
            TextBox input = dialog.AddText("权利人姓名 *", ownerName,
                "请输入个人姓名或单位名称");
            dialog.AcceptText = "下一步";
            dialog.Validate = delegate
            {
                return string.IsNullOrWhiteSpace(input.Text)
                    ? "权利人姓名不能为空。" : string.Empty;
            };
            if (!dialog.ShowForAutoCad()) return false;
            ownerName = (input.Text ?? string.Empty).Trim();
            return ownerName.Length > 0;
        }

        public static bool TryGetNumberingDirection(bool initialClockwise,
            out bool clockwise)
        {
            clockwise = initialClockwise;
            var dialog = new BoundaryFloatingDialog("选择界址点编号方向",
                "界址点号将从已输入的起点开始连续生成。", 410);
            ComboBox direction = dialog.AddCombo("编号方向",
                new[] { "顺时针", "逆时针" },
                initialClockwise ? "顺时针" : "逆时针");
            dialog.AcceptText = "应用并填入";
            if (!dialog.ShowForAutoCad()) return false;
            clockwise = string.Equals(direction.SelectedItem as string,
                "顺时针", StringComparison.Ordinal);
            return true;
        }

        public static bool TryGetRegionName(string title, string current,
            out string name)
        {
            name = (current ?? string.Empty).Trim();
            var dialog = new BoundaryFloatingDialog(
                string.IsNullOrWhiteSpace(title) ? "地籍调查区域" : title,
                "区域名称用于区分同一张图纸内的宗地调查数据。", 430);
            TextBox input = dialog.AddText("区域名称 *", name,
                "例如 宗地 1、北侧宗地");
            dialog.AcceptText = "确定";
            dialog.Validate = delegate
            {
                return string.IsNullOrWhiteSpace(input.Text)
                    ? "区域名称不能为空。" : string.Empty;
            };
            if (!dialog.ShowForAutoCad()) return false;
            name = (input.Text ?? string.Empty).Trim();
            return name.Length > 0;
        }

        public static ParcelBoundarySegmentRecord EditSegment(
            ParcelBoundaryRangeSelection range,
            ParcelBoundarySegmentRecord existing)
        {
            existing = existing ?? new ParcelBoundarySegmentRecord();
            var dialog = new BoundaryFloatingDialog("填写界址段信息",
                RangeSummary(range) + "；保存后可继续选择下一段，Esc 退出。",
                510);
            ComboBox category = dialog.AddCombo("界址线类别 *",
                LineCategories, existing.LineCategory);
            ComboBox position = dialog.AddCombo("界址线位置 *",
                LinePositions, existing.LinePosition == "待确认"
                    ? string.Empty : existing.LinePosition);
            TextBox neighborOwner = dialog.AddText("相邻权利人或地物",
                existing.NeighborOwner, "例如 李老七、2 米巷道");
            TextBox description = dialog.AddTextArea("段说明",
                existing.Description);
            dialog.AcceptText = "保存并继续选择";
            dialog.Validate = delegate
            {
                if (category.SelectedItem == null) return "请选择界址线类别。";
                if (position.SelectedItem == null) return "请选择界址线位置。";
                return string.Empty;
            };
            if (!dialog.ShowForAutoCad()) return null;
            return new ParcelBoundarySegmentRecord
            {
                StartPointNumber = range.StartPointNumber,
                MiddlePointNumbers = range.MiddlePointNumbers,
                EndPointNumber = range.EndPointNumber,
                Distance = range.Distance,
                Direction = range.Direction,
                LineCategory = category.SelectedItem as string ?? string.Empty,
                LinePosition = position.SelectedItem as string ?? string.Empty,
                // 界址段录入不再维护邻宗代码；历史/导入数据保留在
                // 业务模型中，后续签章分组仍可按自己的字段处理。
                NeighborParcelCode = existing.NeighborParcelCode
                    ?? string.Empty,
                NeighborOwner = (neighborOwner.Text ?? string.Empty).Trim(),
                Description = (description.Text ?? string.Empty).Trim(),
                Status = ParcelFieldStatus.Manual,
                Confirmed = true,
                NeighborHandled = true
            };
        }

        public static ParcelBoundarySignatureGroupRecord EditSignature(
            ParcelBoundaryRangeSelection range,
            ParcelBoundarySignatureGroupRecord existing,
            ParcelBoundarySegmentRecord segment, string parcelRepresentative)
        {
            existing = existing ?? new ParcelBoundarySignatureGroupRecord();
            segment = segment ?? new ParcelBoundarySegmentRecord();
            var dialog = new BoundaryFloatingDialog("填写界址签章组信息",
                "起点 " + range.StartPointNumber + " · 中间点 "
                + ParcelBoundaryPointNumberFormatter.FormatSignatureMiddle(
                    range.MiddlePointNumbers) + " · 终点 "
                + range.EndPointNumber + "；保存后可继续选择下一段，Esc 退出。",
                520);
            TextBox owner = dialog.AddText("相邻宗地权利人",
                First(existing.NeighborOwner, segment.NeighborOwner), string.Empty);
            TextBox code = dialog.AddText("相邻宗地代码",
                First(existing.NeighborParcelCode, segment.NeighborParcelCode),
                string.Empty);
            TextBox neighborRepresentative = dialog.AddText("邻宗指界人姓名",
                existing.NeighborRepresentative, string.Empty);
            TextBox currentRepresentative = dialog.AddText("本宗指界人姓名",
                First(existing.ParcelRepresentative, parcelRepresentative),
                string.Empty);
            TextBox date = dialog.AddText("指界日期",
                existing.ConfirmationDate,
                "yyyy-MM-dd");
            ComboBox status = dialog.AddCombo("签章状态",
                new[] { "待签章", "已签章", "无需签章" },
                First(existing.SignatureStatus, "待签章"));
            CheckBox paperBlank = dialog.AddCheck("保留空白供纸质签章",
                existing.PreservePaperSignatureBlank
                    || string.IsNullOrWhiteSpace(existing.SignatureStatus));
            dialog.AcceptText = "保存并继续选择";
            if (!dialog.ShowForAutoCad()) return null;
            return new ParcelBoundarySignatureGroupRecord
            {
                StartPointNumber = range.StartPointNumber,
                MiddlePointNumbers = ParcelBoundaryPointNumberFormatter
                    .FormatSignatureMiddle(range.MiddlePointNumbers),
                EndPointNumber = range.EndPointNumber,
                NeighborOwner = (owner.Text ?? string.Empty).Trim(),
                NeighborParcelCode = (code.Text ?? string.Empty).Trim(),
                NeighborRepresentative = (neighborRepresentative.Text
                    ?? string.Empty).Trim(),
                ParcelRepresentative = (currentRepresentative.Text
                    ?? string.Empty).Trim(),
                ConfirmationDate = (date.Text ?? string.Empty).Trim(),
                SignatureStatus = status.SelectedItem as string ?? "待签章",
                PreservePaperSignatureBlank = paperBlank.IsChecked == true,
                Confirmed = true,
                Status = ParcelFieldStatus.Manual
            };
        }

        private static string RangeSummary(ParcelBoundaryRangeSelection range)
        {
            string middle = string.IsNullOrWhiteSpace(range.MiddlePointNumbers)
                ? string.Empty : " → " + range.MiddlePointNumbers;
            return range.StartPointNumber + middle + " → "
                + range.EndPointNumber + " · "
                + range.Distance.ToString("0.##", CultureInfo.InvariantCulture)
                + " m · " + range.Direction + "方向";
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty
                : value;
        }

        private sealed class BoundaryFloatingDialog : Window
        {
            private readonly StackPanel _fields;
            private readonly TextBlock _error;
            private readonly Button _accept;

            public BoundaryFloatingDialog(string title, string subtitle,
                double width)
            {
                Title = title;
                Width = width;
                SizeToContent = SizeToContent.Height;
                MaxHeight = 760;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                FontFamily = new FontFamily("Microsoft YaHei UI");
                FontSize = 12.5;

                var card = new Border
                {
                    Margin = new Thickness(18),
                    Padding = new Thickness(16),
                    CornerRadius = new CornerRadius(10),
                    Background = Brushes.White,
                    BorderBrush = Brush("#BFD1E7"),
                    BorderThickness = new Thickness(1),
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 26,
                        ShadowDepth = 0,
                        Opacity = 0.28,
                        Color = Color.FromRgb(45, 98, 170)
                    }
                };
                Content = card;
                var root = new StackPanel();
                card.Child = root;

                var header = new Grid { Cursor = Cursors.SizeAll };
                header.ColumnDefinitions.Add(new ColumnDefinition());
                header.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = GridLength.Auto });
                var titles = new StackPanel();
                titles.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#172033")
                });
                titles.Children.Add(new TextBlock
                {
                    Text = subtitle ?? string.Empty,
                    Margin = new Thickness(0, 5, 12, 0),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = Brush("#66758B")
                });
                header.Children.Add(titles);
                Button close = SmallButton("×");
                close.Width = 30;
                close.Click += delegate { DialogResult = false; };
                Grid.SetColumn(close, 1);
                header.Children.Add(close);
                header.MouseLeftButtonDown += delegate
                {
                    try { DragMove(); } catch { }
                };
                root.Children.Add(header);

                _fields = new StackPanel { Margin = new Thickness(0, 14, 0, 4) };
                root.Children.Add(_fields);
                _error = new TextBlock
                {
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 6, 0, 0),
                    Foreground = Brush("#B42318"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                };
                root.Children.Add(_error);

                var footer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 14, 0, 0)
                };
                Button cancel = ActionButton("取消", false);
                cancel.Click += delegate { DialogResult = false; };
                footer.Children.Add(cancel);
                _accept = ActionButton("确定", true);
                _accept.Margin = new Thickness(8, 0, 0, 0);
                _accept.Click += delegate { AcceptDialog(); };
                footer.Children.Add(_accept);
                root.Children.Add(footer);
                PreviewKeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.Key == Key.Escape)
                    {
                        DialogResult = false;
                        e.Handled = true;
                        return;
                    }
                    if (e.Key != Key.Enter
                        || Keyboard.Modifiers != ModifierKeys.None) return;
                    TextBox source = e.OriginalSource as TextBox;
                    if (source != null && source.AcceptsReturn) return;
                    AcceptDialog();
                    e.Handled = true;
                };
            }

            public Func<string> Validate { get; set; }
            public string AcceptText
            {
                set { _accept.Content = value ?? "确定"; }
            }

            private void AcceptDialog()
            {
                string message = Validate == null
                    ? string.Empty : Validate() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _error.Text = message;
                    _error.Visibility = Visibility.Visible;
                    return;
                }
                DialogResult = true;
            }

            public TextBox AddText(string label, string value,
                string placeholder)
            {
                TextBox box = TextBox(value, false);
                if (!string.IsNullOrWhiteSpace(placeholder))
                    box.ToolTip = placeholder;
                AddField(label, box);
                return box;
            }

            public TextBox AddTextArea(string label, string value)
            {
                TextBox box = TextBox(value, true);
                box.MinHeight = 70;
                box.MaxHeight = 150;
                AddField(label, box);
                return box;
            }

            public ComboBox AddCombo(string label, IEnumerable<string> items,
                string selected)
            {
                var combo = new ComboBox
                {
                    Height = 34,
                    Padding = new Thickness(8, 3, 8, 3),
                    BorderBrush = Brush("#D2DDEA"),
                    Background = Brushes.White
                };
                foreach (string item in items) combo.Items.Add(item);
                if (!string.IsNullOrWhiteSpace(selected)
                    && combo.Items.Contains(selected)) combo.SelectedItem = selected;
                AddField(label, combo);
                return combo;
            }

            public CheckBox AddCheck(string label, bool value)
            {
                var check = new CheckBox
                {
                    Content = label,
                    IsChecked = value,
                    Margin = new Thickness(0, 7, 0, 1),
                    Foreground = Brush("#31415A")
                };
                _fields.Children.Add(check);
                return check;
            }

            public bool ShowForAutoCad()
            {
                IntPtr owner = AcadApp.MainWindow == null
                    ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(this).Owner = owner;
                return AcadApp.ShowModalWindow(this) == true;
            }

            private void AddField(string label, Control control)
            {
                _fields.Children.Add(new TextBlock
                {
                    Text = label,
                    Margin = new Thickness(0, 7, 0, 5),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#31415A")
                });
                _fields.Children.Add(control);
            }

            private static TextBox TextBox(string value, bool multiline)
            {
                return new TextBox
                {
                    Text = value ?? string.Empty,
                    Height = multiline ? double.NaN : 34,
                    Padding = new Thickness(8, 6, 8, 6),
                    BorderBrush = Brush("#D2DDEA"),
                    Background = Brushes.White,
                    AcceptsReturn = multiline,
                    TextWrapping = multiline ? TextWrapping.Wrap
                        : TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = multiline
                        ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden
                };
            }

            private static Button ActionButton(string text, bool primary)
            {
                return new Button
                {
                    Content = text,
                    MinWidth = 86,
                    Height = 34,
                    Padding = new Thickness(13, 0, 13, 0),
                    BorderThickness = new Thickness(1),
                    BorderBrush = primary ? TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.AccentBrush
                        : Brush("#D2DDEA"),
                    Background = primary ? TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.AccentBrush : Brushes.White,
                    Foreground = primary ? TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.OnAccentBrush : Brush("#31415A"),
                    FontWeight = FontWeights.SemiBold
                };
            }

            private static Button SmallButton(string text)
            {
                return new Button
                {
                    Content = text,
                    Height = 28,
                    Padding = new Thickness(8, 0, 8, 0),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = Brush("#66758B"),
                    FontSize = 18
                };
            }

            private static SolidColorBrush Brush(string color)
            {
                return new SolidColorBrush((Color)ColorConverter
                    .ConvertFromString(color));
            }
        }
    }
}
