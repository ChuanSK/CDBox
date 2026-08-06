using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// CAD ???????? AutoCAD ActiveX COM ????????? Interop ???
    ///
    /// ???????
    /// 1. ????????? AddSubMenu(Index, Label) ? AddSubMenu(Index, Label, Tag)???????????????
    /// 2. ??????????????? Count ????????? CASS ???? 0/1 ??????
    /// 3. ???????? PopupMenu?CASS/AutoCAD ????? CUI ? PopupMenu ??????????????????????
    /// 4. CDMENUDUMP ????????????????????????
    /// </summary>
    internal static class CDBoxMenuService
    {
        private const string TopMenuName = "??????";
        private const string EmptyText = "????";

        // ???? Ctrl+C ??????? CASS ? "^C^C" ???????????
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

                // ????????????????????????
                RemoveTopMenusFromMenuBar(acad);

                object topMenu = FindPopupMenu(popupMenus, TopMenuName);
                if (topMenu == null)
                {
                    topMenu = Invoke(popupMenus, "Add", TopMenuName);
                }

                if (topMenu == null) return;

                // ??????????????????????
                bool cleared = ClearPopupMenuItems(topMenu);
                if (!cleared && GetCount(topMenu) > 0)
                {
                    // ?????????????????? Add ???????
                    // ?? CASS ?????????????????????????????????????????????
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

                // ??????? CUI ??? CASS ????????????????
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

                // ???????????????? CUI ???????? CASS/ACAD ?????
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
                if (acad == null) return "???? AutoCAD Application?";

                sb.AppendLine("[CDBox ????]");

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
                sb.AppendLine("?????" + ex.Message);
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

                // AutoCAD ActiveX ???????????
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

            // ???????????????????
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
                       .Replace("?", string.Empty)
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

            // ?? 0 ??1 ???????? count/count+1 ????????????????? Count?
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
            object survey = AddSubMenu(topMenu, "????",
                "CDBox_Survey");
            AddCommandItem(survey, "????", "CDJMSB");
            AddCommandItem(survey, "??????", "CDJMSZ");
            AddSeparator(topMenu);

            // ActiveX PopupMenu ??? AutoCAD/CASS ???????????????
            // ???????????????????????
            AddCommandItem(topMenu, "? ?????", "CDLAYER");
            AddSeparator(topMenu);

            object annotation = AddSubMenu(topMenu, "? ??", "CDBox_Annotation");
            AddCommandItem(annotation, "? ?????", "CDSURF");
            AddCommandItem(annotation, "? ??????", "CDLEN");
            AddCommandItem(annotation, "? ????", "CDNODE");
            AddSeparator(annotation);
            AddCommandItem(annotation, "? ????", "CDBZSET");

            object section = AddSubMenu(topMenu, "? ??", "CDBox_Section");
            AddCommandItem(section, "? ?????", "CDSEC");
            AddCommandItem(section, "? ??????", "PLDM");
            AddSeparator(section);
            AddCommandItem(section, "? ?????", "ZDM");
            AddCommandItem(section, "? ?????", "ZDMSZ");

            object pipeAttribute = AddSubMenu(topMenu, "? ????", "CDBox_PipeAttribute");
            AddCommandItem(pipeAttribute, "? ?????", "SX");
            AddCommandItem(pipeAttribute, "? ????", "SXQC");
            AddSeparator(pipeAttribute);
            AddCommandItem(pipeAttribute, "? ?????", "SXMRB");
            AddSeparator(topMenu);

            object quantity = AddSubMenu(topMenu, "? ???", "CDBox_Quantity");
            AddCommandItem(quantity, "? ?????", "CDQBOARD");
            AddCommandItem(quantity, "? ???????", "GCL");

            object frame = AddSubMenu(topMenu, "? ????", "CDBox_Frame");
            AddCommandItem(frame, "? ??????", "TCFRAMEADD");
            AddCommandItem(frame, "? ??????", "TCFRAMECUT");
            AddCommandItem(frame, "? ??????", "TCFRAMELAYOUT");
            AddCommandItem(frame, "? ??????", "TCFRAMEPLACE");
            AddCommandItem(frame, "? ????", "TCFRAMESET");

            object table = AddSubMenu(topMenu, "? ????", "CDBox_Table");
            AddCommandItem(table, "? Excel ? CAD ??", "CDEXCEL");
            AddSeparator(topMenu);

            AddCommandItem(topMenu, "? CDBox ???", "CDSTUDIO");
            AddCommandItem(topMenu, "? CDBox??", "CDSET");
            AddSeparator(topMenu);
            AddCommandItem(topMenu, "? ????????", "CDABOUT");
        }

        private static object AddSubMenu(object parent, string label, string tag)
        {
            if (parent == null) return null;

            object result = InvokeSubMenuAdd(parent, label, tag);
            if (result == null) return null;

            // AddSubMenu ?????? PopupMenuItem???????? SubMenu ????
            object subMenu = GetProperty(result, "SubMenu") ?? GetProperty(result, "Submenu");
            if (subMenu != null) return subMenu;

            // ???????????? PopupMenu?
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

            // CASS/AutoCAD ????? ActiveX ??????????
            // - ???? AddSubMenu ?? Index + Label?
            // - ?????? Index + Label + Tag?
            // - ?? CASS ????????????????????
            // ????????????????????????
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
            // ????????????? CASS11 ???????????????????????
            object result = Invoke(parent, "AddSubMenu", index, label);
            if (result != null) return result;

            result = Invoke(parent, "AddSubmenu", index, label);
            if (result != null) return result;

            // ????? Tag ????
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
