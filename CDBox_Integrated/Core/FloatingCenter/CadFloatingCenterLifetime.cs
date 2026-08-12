using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    internal static class CadFloatingCenterLifetime
    {
        private static readonly object Gate = new object();
        private static IFloatingCenter _center;
        private static bool _initialized;

        public static void Initialize(IFloatingCenter center)
        {
            if (center == null) throw new ArgumentNullException("center");
            lock (Gate)
            {
                if (_initialized) return;
                _center = center;
                try
                {
                    AcadApp.DocumentManager.DocumentToBeDestroyed +=
                        DocumentToBeDestroyed;
                    _initialized = true;
                }
                catch
                {
                    _center = null;
                }
            }
        }

        public static void Terminate()
        {
            lock (Gate)
            {
                if (_initialized)
                {
                    try
                    {
                        AcadApp.DocumentManager.DocumentToBeDestroyed -=
                            DocumentToBeDestroyed;
                    }
                    catch { }
                }
                _initialized = false;
                _center = null;
            }
        }

        private static void DocumentToBeDestroyed(object sender,
            DocumentCollectionEventArgs e)
        {
            Document document = e == null ? null : e.Document;
            if (document == null) return;
            try
            {
                IFloatingCenter center;
                lock (Gate) center = _center;
                if (center != null)
                    center.ClearDocument(
                        CadFloatingDocumentIdentity.GetDocumentId(document));
            }
            catch { }
            finally
            {
                CadFloatingDocumentIdentity.Release(document);
            }
        }
    }
}
