using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.Modules;

using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioHost
    {
        private static CDBoxStudioForm _form;

        public static void Show(IEnumerable<ITCModule> modules)
        {
            IList<CDBoxStudioAction> actions = CDBoxStudioActionFactory.Create(modules);
            CDBoxStudioLogger.Info("请求打开 Studio。动作数量：" + actions.Count);

            if (_form != null && !_form.IsDisposed)
            {
                _form.SetActions(actions);
                if (!_form.Visible) _form.Show(new AcadMainWindow());
                _form.WindowState = FormWindowState.Normal;
                _form.Activate();
                return;
            }

            _form = new CDBoxStudioForm(actions);
            _form.FormClosed += delegate
            {
                CDBoxStudioLogger.Info("Studio 窗口已关闭。 ");
                _form = null;
            };
            _form.Show(new AcadMainWindow());
        }
    }
}
