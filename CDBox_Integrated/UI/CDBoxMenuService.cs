using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// CAD 菜单栏入口。使用 AutoCAD ActiveX COM 后期绑定，避免增加 Interop 引用。
    ///
    /// 本版修复重点：
    /// 1. 子菜单创建同时兼容 AddSubMenu(Index, Label) 与 AddSubMenu(Index, Label, Tag)，并优先尝试空字符串索引追加。
    /// 2. 旧菜单项清理改为“逐个删除直到 Count 下降”的方式，兼容 CASS 菜单集合 0/1 基索引差异。
    /// 3. 不再依赖删除整个 PopupMenu。CASS/AutoCAD 对已经写入 CUI 的 PopupMenu 删除并不稳定；优先清空旧菜单内容后原位重建。
    /// 4. CDMENUDUMP 会输出一级、二级菜单明细，用于确认真实菜单结构。
    /// </summary>
    internal static class CDBoxMenuService
    {
        private const string TopMenuName = "超重氢工具箱";
        private const string EmptyText = "暂无功能";

        // 使用真实 Ctrl+C 控制字符，避免 CASS 将 "^C^C" 当作普通命令文本执行。
        private const string CancelMacroPrefix = "\x03\x03";

        private static bool _menuBuiltInCurrentSession;
        private static bool _isEnsuringMenu;

        public static void EnsureMenu(bool forceRebuild = false)
        {
            if (_isEnsuringMenu) return;

            if (_menuBuiltInCurrentSession && !forceRebuild)
            {
                TryShowMenuBar();
                return;
            }

            _isEnsuringMenu = true;
            try
            {
                object acad = GetAcadApplication();
                if (acad == null) return;

                TryShowMenuBar();

                object primaryMenuGroup = GetPrimaryMenuGroup(acad);
                if (primaryMenuGroup == null) return;

                object popupMenus = GetProperty(primaryMenuGroup, "Menus");
                if (popupMenus == null) return;

                // 先从菜单栏摘下旧入口，避免重建过程中显示旧对象。
                RemoveTopMenusFromMenuBar(acad);

                object topMenu = FindPopupMenu(popupMenus, TopMenuName);
                if (topMenu == null)
                {
                    topMenu = Invoke(popupMenus, "Add", TopMenuName);
                }

                if (topMenu == null) return;

                // 核心：不要继续追加到旧菜单，必须先清空旧项。
                bool cleared = ClearPopupMenuItems(topMenu);
                if (!cleared && GetCount(topMenu) > 0)
                {
                    // 如果旧对象仍无法清空，改名隔离后重新 Add 一个干净菜单。
                    // 有些 CASS 环境禁止删除主菜单对象，但允许改名；若改名失败，则保留诊断信息，不继续追加造成更乱的结构。
                    TryInvoke(topMenu, "RemoveFromMenuBar");
                    bool renamed = TryRenameMenu(topMenu, "_CDBox_OldMenu_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                    if (renamed)
                    {
                        topMenu = Invoke(popupMenus, "Add", TopMenuName);
                        if (topMenu == null) return;
                        ClearPopupMenuItems(topMenu);
                    }
                    else
                    {
                        return;
                    }
                }

                BuildMenu(topMenu);
                InsertMenuInMenuBar(acad, topMenu);
                TryShowMenuBar();
                TryUpdateAcad(acad);

                // 保存菜单组。若 CUI 只读或 CASS 拒绝保存，忽略，不影响当前会话。
                TrySaveMenuGroup(primaryMenuGroup);

                _menuBuiltInCurrentSession = true;
            }
            finally
            {
                _isEnsuringMenu = false;
            }
        }

        public static void ResetMenu()
        {
            _menuBuiltInCurrentSession = false;
            EnsureMenu(true);
        }

        public static void RemoveMenu(bool saveMenuGroup = false)
        {
            try
            {
                object acad = GetAcadApplication();
                if (acad == null) return;

                object primaryMenuGroup = GetPrimaryMenuGroup(acad);
                RemoveTopMenusFromMenuBar(acad);

                // 卸载时只从菜单栏移除，不强行删除 CUI 内对象，避免破坏 CASS/ACAD 主菜单组。
                TryUpdateAcad(acad);
                if (saveMenuGroup && primaryMenuGroup != null) TrySaveMenuGroup(primaryMenuGroup);
                _menuBuiltInCurrentSession = false;
            }
            catch
            {
            }
        }

        public static string GetDiagnosticText()
        {
            var sb = new StringBuilder();
            try
            {
                object acad = GetAcadApplication();
                if (acad == null) return "未获取到 AutoCAD Application。";

                sb.AppendLine("[CDBox 菜单诊断]");

                object menuBar = GetProperty(acad, "MenuBar");
                sb.AppendLine("MenuBar.Count=" + GetCount(menuBar));
                AppendCollectionNames(sb, "MenuBar", menuBar, true, true);

                object menuGroups = GetProperty(acad, "MenuGroups");
                int groupCount = GetCount(menuGroups);
                sb.AppendLine("MenuGroups.Count=" + groupCount);

                for (int i = 0; i < groupCount; i++)
                {
                    object group = GetItem(menuGroups, i);
                    if (group == null) continue;

                    string groupName = GetStringProperty(group, "Name");
                    object menus = GetProperty(group, "Menus");
                    sb.AppendLine("Group[" + i + "]=" + groupName + ", Menus.Count=" + GetCount(menus));
                    AppendCollectionNames(sb, "  Menus", menus, true, true);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("诊断失败：" + ex.Message);
            }

            return sb.ToString();
        }

        private static object GetAcadApplication()
        {
            try
            {
                return AcadApp.AcadApplication;
            }
            catch
            {
                return null;
            }
        }

        private static object GetPrimaryMenuGroup(object acad)
        {
            object menuGroups = GetProperty(acad, "MenuGroups");
            if (menuGroups == null) return null;

            int count = GetCount(menuGroups);
            if (count <= 0) return null;

            object first = null;
            for (int i = 0; i < count; i++)
            {
                object group = GetItem(menuGroups, i);
                if (group == null) continue;
                if (first == null) first = group;

                string name = GetStringProperty(group, "Name");
                if (!string.IsNullOrWhiteSpace(name) && name.IndexOf("ACAD", StringComparison.OrdinalIgnoreCase) >= 0)
                    return group;
            }

            return first;
        }

        private static void RemoveTopMenusFromMenuBar(object acad)
        {
            object menuBar = GetProperty(acad, "MenuBar");
            if (menuBar == null) return;

            for (int pass = 0; pass < 64; pass++)
            {
                int beforeCount = GetCount(menuBar);
                bool touched = false;

                // AutoCAD ActiveX 支持直接按名称取菜单。
                object byName = Invoke(menuBar, "Item", TopMenuName);
                if (IsTopMenu(byName))
                {
                    touched = TryInvoke(byName, "RemoveFromMenuBar") || TryInvoke(byName, "RemoveMenuFromMenuBar") || touched;
                }

                int count = GetCount(menuBar);
                for (int i = count; i >= 0; i--)
                {
                    object item = Invoke(menuBar, "Item", i);
                    if (!IsTopMenu(item)) continue;
                    touched = TryInvoke(item, "RemoveFromMenuBar") || TryInvoke(item, "RemoveMenuFromMenuBar") || touched;
                }

                int afterCount = GetCount(menuBar);
                if (!touched || afterCount >= beforeCount) break;
            }
        }

        private static List<object> FindPopupMenus(object popupMenus, string menuName)
        {
            var result = new List<object>();
            if (popupMenus == null || string.IsNullOrWhiteSpace(menuName)) return result;

            int count = GetCount(popupMenus);
            for (int i = 0; i <= count; i++)
            {
                object menu = Invoke(popupMenus, "Item", i);
                if (IsSameMenuName(GetMenuDisplayText(menu), menuName)) result.Add(menu);
            }

            // 部分环境支持名称索引但索引枚举不完整。
            object byName = Invoke(popupMenus, "Item", menuName);
            if (IsSameMenuName(GetMenuDisplayText(byName), menuName) && !ContainsSameComObject(result, byName)) result.Add(byName);

            return result;
        }

        private static object FindPopupMenu(object popupMenus, string menuName)
        {
            List<object> found = FindPopupMenus(popupMenus, menuName);
            return found.Count > 0 ? found[0] : null;
        }

        private static bool ContainsSameComObject(List<object> list, object value)
        {
            if (value == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value)) return true;
            }
            return false;
        }

        private static bool IsTopMenu(object menu)
        {
            return IsSameMenuName(GetMenuDisplayText(menu), TopMenuName);
        }

        private static string GetMenuDisplayText(object menuOrItem)
        {
            if (menuOrItem == null) return string.Empty;

            string[] names = { "NameOnMenuBar", "NameNoMnemonic", "Name", "Label", "Caption" };
            for (int i = 0; i < names.Length; i++)
            {
                string value = GetStringProperty(menuOrItem, names[i]);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            return string.Empty;
        }

        private static bool IsSameMenuName(string actual, string expected)
        {
            actual = NormalizeMenuText(actual);
            expected = NormalizeMenuText(expected);
            return !string.IsNullOrEmpty(actual)
                   && !string.IsNullOrEmpty(expected)
                   && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMenuText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Replace("&", string.Empty)
                       .Replace("...", string.Empty)
                       .Replace("…", string.Empty)
                       .Replace("\t", string.Empty)
                       .Replace("\r", string.Empty)
                       .Replace("\n", string.Empty)
                       .Trim();
        }

        private static bool ClearPopupMenuItems(object popupMenu)
        {
            if (popupMenu == null) return false;

            for (int pass = 0; pass < 256; pass++)
            {
                int before = GetCount(popupMenu);
                if (before <= 0) return true;

                bool deletedOne = DeleteOneMenuItem(popupMenu, before);
                int after = GetCount(popupMenu);

                if (after <= 0) return true;
                if (after < before) continue;
                if (!deletedOne) return false;
            }

            return GetCount(popupMenu) <= 0;
        }

        private static bool DeleteOneMenuItem(object popupMenu, int countBefore)
        {
            if (popupMenu == null || countBefore <= 0) return false;

            // 覆盖 0 基、1 基、官方示例中的 count/count+1 等差异。每轮只删一个，删完后重新取 Count。
            int[] candidateIndexes = { 0, 1, countBefore - 1, countBefore, countBefore + 1 };
            for (int i = 0; i < candidateIndexes.Length; i++)
            {
                int index = candidateIndexes[i];
                if (index < 0) continue;

                object item = Invoke(popupMenu, "Item", index);
                if (item == null) continue;

                object subMenu = GetProperty(item, "SubMenu") ?? GetProperty(item, "Submenu");
                if (subMenu != null) ClearPopupMenuItems(subMenu);

                if (TryInvoke(item, "Delete") || TryInvoke(item, "delete")) return true;
            }

            return false;
        }

        private static void BuildMenu(object topMenu)
        {
            object survey = AddSubMenu(topMenu, "测绘工具",
                "CDBox_Survey");
            AddCommandItem(survey, "简码识别", "CDJMSB");
            AddCommandItem(survey, "简码识别设置", "CDJMSZ");
            AddSeparator(topMenu);

            // ActiveX PopupMenu 在不同 AutoCAD/CASS 版本中没有稳定的位图图标接口，
            // 使用菜单字体可直接显示的单色符号作为兼容图标。
            AddCommandItem(topMenu, "▦ 图层管理器", "CDLAYER");
            AddSeparator(topMenu);

            object annotation = AddSubMenu(topMenu, "✎ 标注", "CDBox_Annotation");
            AddCommandItem(annotation, "▱ 表面积标注", "CDSURF");
            AddCommandItem(annotation, "⌁ 管线长度标注", "CDLEN");
            AddCommandItem(annotation, "◇ 节点标注", "CDNODE");
            AddSeparator(annotation);
            AddCommandItem(annotation, "⚙ 标注设置", "CDBZSET");

            object section = AddSubMenu(topMenu, "◫ 断面", "CDBox_Section");
            AddCommandItem(section, "▧ 断面图生成", "CDSEC");
            AddCommandItem(section, "▦ 批量断面生成", "PLDM");
            AddSeparator(section);
            AddCommandItem(section, "▥ 纵断面生成", "CDZDM");
            AddCommandItem(section, "⚙ 纵断面设置", "CDZDMSZ");

            object pipeAttribute = AddSubMenu(topMenu, "◇ 管线属性", "CDBox_PipeAttribute");
            AddCommandItem(pipeAttribute, "✎ 属性编辑器", "SX");
            AddCommandItem(pipeAttribute, "× 属性清除", "SXQC");
            AddSeparator(pipeAttribute);
            AddCommandItem(pipeAttribute, "▤ 属性默认表", "SXMRB");
            AddSeparator(topMenu);

            object quantity = AddSubMenu(topMenu, "Σ 工程量", "CDBox_Quantity");
            AddCommandItem(quantity, "▣ 工程量看板", "CDQBOARD");
            AddCommandItem(quantity, "▤ 工程量表格生成", "GCL");

            object frame = AddSubMenu(topMenu, "▣ 图框工具", "CDBox_Frame");
            AddCommandItem(frame, "＋ 添加图框模版", "TCFRAMEADD");
            AddCommandItem(frame, "▱ 布置裁图区域", "TCFRAMECUT");
            AddCommandItem(frame, "▦ 裁图区域布框", "TCFRAMELAYOUT");
            AddCommandItem(frame, "□ 直接布置图框", "TCFRAMEPLACE");
            AddCommandItem(frame, "⚙ 图框设置", "TCFRAMESET");

            object table = AddSubMenu(topMenu, "▤ 表格工具", "CDBox_Table");
            AddCommandItem(table, "▦ Excel 转 CAD 表格", "CDEXCEL");
            AddSeparator(topMenu);

            AddCommandItem(topMenu, "▦ CDBox 工作台", "CDSTUDIO");
            AddCommandItem(topMenu, "⚙ CDBox设置", "CDSET");
            AddSeparator(topMenu);
            AddCommandItem(topMenu, "ⓘ 关于超重氢工具箱", "CDABOUT");
        }

        private static object AddSubMenu(object parent, string label, string tag)
        {
            if (parent == null) return null;

            object result = InvokeSubMenuAdd(parent, label, tag);
            if (result == null) return null;

            // AddSubMenu 标准返回值是 PopupMenuItem，真正的子菜单在 SubMenu 属性中。
            object subMenu = GetProperty(result, "SubMenu") ?? GetProperty(result, "Submenu");
            if (subMenu != null) return subMenu;

            // 某些包装器可能直接返回子 PopupMenu。
            if (GetProperty(result, "Count") != null) return result;

            return null;
        }

        private static object AddCommandItem(object parent, string label, string commandName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(commandName)) return null;
            return InvokeCommandItemAdd(parent, label, BuildCommandMacro(commandName));
        }

        private static string BuildCommandMacro(string commandName)
        {
            return CancelMacroPrefix + commandName.Trim() + " ";
        }

        private static void AddEmptyItem(object parent)
        {
            if (parent == null) return;

            object item = InvokeCommandItemAdd(parent, EmptyText, string.Empty);
            if (item == null) return;

            TrySetProperty(item, "Macro", string.Empty);
            if (!TrySetProperty(item, "Enable", false)) TrySetProperty(item, "Enabled", false);
        }

        private static object InvokeSubMenuAdd(object parent, string label, string tag)
        {
            if (parent == null) return null;

            int count = GetCount(parent);

            // CASS/AutoCAD 不同版本的 ActiveX 包装表现不完全一致：
            // - 有的版本 AddSubMenu 使用 Index + Label；
            // - 有的版本支持 Index + Label + Tag；
            // - 部分 CASS 环境对“追加到末尾”更接受空字符串索引。
            // 因此这里按“最少破坏、最大兼容”的顺序逐一尝试。
            object[] indexes = { string.Empty, count, count + 1, Math.Max(0, count - 1), 0, 1 };
            for (int i = 0; i < indexes.Length; i++)
            {
                object result = TryAddSubMenuWithAllSignatures(parent, indexes[i], label, tag);
                if (result != null) return result;
            }

            return null;
        }

        private static object TryAddSubMenuWithAllSignatures(object parent, object index, string label, string tag)
        {
            // 先尝试两参数写法。当前南方 CASS11 环境中，三参数写法会失败，导致只剩一级命令项。
            object result = Invoke(parent, "AddSubMenu", index, label);
            if (result != null) return result;

            result = Invoke(parent, "AddSubmenu", index, label);
            if (result != null) return result;

            // 再兼容需要 Tag 的版本。
            result = Invoke(parent, "AddSubMenu", index, label, tag);
            if (result != null) return result;

            result = Invoke(parent, "AddSubmenu", index, label, tag);
            if (result != null) return result;

            return null;
        }

        private static object InvokeCommandItemAdd(object parent, string label, string macro)
        {
            if (parent == null) return null;

            int count = GetCount(parent);
            int[] indexes = { count, count + 1, Math.Max(0, count - 1), 0, 1 };
            for (int i = 0; i < indexes.Length; i++)
            {
                object result = Invoke(parent, "AddMenuItem", indexes[i], label, macro);
                if (result != null) return result;
            }

            return null;
        }

        private static object AddSeparator(object parent)
        {
            if (parent == null) return null;

            int count = GetCount(parent);
            object[] indexes =
            {
                string.Empty, count, count + 1, Math.Max(0, count - 1), 0, 1
            };
            for (int i = 0; i < indexes.Length; i++)
            {
                int before = GetCount(parent);
                object result = Invoke(parent, "AddSeparator", indexes[i]);
                if (result != null || GetCount(parent) > before)
                    return result ?? parent;
                before = GetCount(parent);
                result = Invoke(parent, "AddMenuSeparator", indexes[i]);
                if (result != null || GetCount(parent) > before)
                    return result ?? parent;
            }

            return null;
        }

        private static void InsertMenuInMenuBar(object acad, object menu)
        {
            if (acad == null || menu == null) return;

            RemoveTopMenusFromMenuBar(acad);

            object menuBar = GetProperty(acad, "MenuBar");
            int index = GetCount(menuBar);

            if (TryInvoke(menu, "InsertInMenuBar", index)) return;
            if (TryInvoke(menu, "InsertInMenuBar", index + 1)) return;
            if (TryInvoke(menu, "InsertInMenuBar", Math.Max(0, index - 1))) return;
            TryInvoke(menu, "InsertInMenuBar", 1);
        }

        private static void TryShowMenuBar()
        {
            try
            {
                object current = AcadApp.GetSystemVariable("MENUBAR");
                int value;
                if (current != null && int.TryParse(Convert.ToString(current), out value) && value == 0)
                {
                    AcadApp.SetSystemVariable("MENUBAR", 1);
                }
            }
            catch
            {
            }
        }

        private static void TryUpdateAcad(object acad)
        {
            TryInvoke(acad, "Update");
        }

        private static bool TryRenameMenu(object menu, string newName)
        {
            if (menu == null || string.IsNullOrWhiteSpace(newName)) return false;
            bool ok = false;
            ok = TrySetProperty(menu, "Name", newName) || ok;
            ok = TrySetProperty(menu, "NameOnMenuBar", newName) || ok;
            ok = TrySetProperty(menu, "Label", newName) || ok;
            return ok;
        }

        private static void TrySaveMenuGroup(object menuGroup)
        {
            if (menuGroup == null) return;
            if (TryInvoke(menuGroup, "Save", 0)) return;
            TryInvoke(menuGroup, "Save");
        }

        private static void AppendCollectionNames(StringBuilder sb, string title, object collection, bool onlyCdBox, bool dumpChildren)
        {
            int count = GetCount(collection);
            for (int i = 0; i <= count; i++)
            {
                object item = Invoke(collection, "Item", i);
                string name = GetMenuDisplayText(item);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (onlyCdBox && !IsSameMenuName(name, TopMenuName)) continue;

                sb.AppendLine(title + "[" + i + "]=" + name + ", Count=" + GetCount(item));
                if (dumpChildren) AppendMenuItems(sb, item, "    ", 0, 2);
            }
        }

        private static void AppendMenuItems(StringBuilder sb, object menu, string indent, int depth, int maxDepth)
        {
            if (menu == null || depth > maxDepth) return;
            int count = GetCount(menu);
            for (int i = 0; i <= count; i++)
            {
                object item = Invoke(menu, "Item", i);
                if (item == null) continue;

                string label = GetMenuDisplayText(item);
                string macro = GetStringProperty(item, "Macro");
                object subMenu = GetProperty(item, "SubMenu") ?? GetProperty(item, "Submenu");
                string arrow = subMenu == null ? string.Empty : " >";
                if (!string.IsNullOrWhiteSpace(label) || subMenu != null)
                {
                    sb.AppendLine(indent + "Item[" + i + "]=" + label + arrow + (string.IsNullOrWhiteSpace(macro) ? string.Empty : ", Macro=" + EscapeForLog(macro)));
                }

                if (subMenu != null) AppendMenuItems(sb, subMenu, indent + "  ", depth + 1, maxDepth);
            }
        }

        private static string EscapeForLog(string text)
        {
            if (text == null) return string.Empty;
            return text.Replace("\x03", "<Ctrl+C>").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static int GetCount(object target)
        {
            if (target == null) return 0;
            object value = GetProperty(target, "Count");
            if (value == null) return 0;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static object GetItem(object collection, int zeroBasedIndex)
        {
            if (collection == null) return null;

            object item = Invoke(collection, "Item", zeroBasedIndex);
            if (item != null) return item;

            item = Invoke(collection, "Item", zeroBasedIndex + 1);
            if (item != null) return item;

            return null;
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            object value = GetProperty(target, propertyName);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static object GetProperty(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName)) return null;
            try
            {
                return target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySetProperty(object target, string propertyName, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName)) return false;
            try
            {
                target.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, target, new[] { value });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName)) return null;
            try
            {
                return target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, null, target, args ?? new object[0]);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName)) return false;
            try
            {
                target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, null, target, args ?? new object[0]);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
