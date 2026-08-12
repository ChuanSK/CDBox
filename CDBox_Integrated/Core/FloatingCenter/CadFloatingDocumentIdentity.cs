using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    internal static class CadFloatingDocumentIdentity
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Document, string> SessionIds =
            new Dictionary<Document, string>(
                new ReferenceComparer<Document>());

        public static string GetCurrentDocumentId()
        {
            return GetDocumentId(AcadApp.DocumentManager.MdiActiveDocument);
        }

        public static string GetDocumentId(Document document)
        {
            if (document == null) return "__global__";
            lock (Gate)
            {
                string value;
                if (SessionIds.TryGetValue(document, out value)) return value;
                string fingerprint = string.Empty;
                try
                {
                    fingerprint = document.Database.FingerprintGuid.ToString()
                        .Replace("-", string.Empty);
                }
                catch { }
                string session = Guid.NewGuid().ToString("N");
                value = string.IsNullOrWhiteSpace(fingerprint)
                    ? "session:" + session
                    : "dwg:" + fingerprint + ":session:" + session;
                SessionIds[document] = value;
                return value;
            }
        }

        public static void Release(Document document)
        {
            if (document == null) return;
            lock (Gate) SessionIds.Remove(document);
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
