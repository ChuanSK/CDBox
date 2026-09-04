namespace CDBox.Shared
{
    internal static class CDBoxRequiredRuntimeFiles
    {
        public static readonly string[] ManagedDependencies =
        {
            "CDBox.Shared.dll",
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll",
            "NPOI.Core.dll",
            "NPOI.OOXML.dll",
            "NPOI.OpenXml4Net.dll",
            "NPOI.OpenXmlFormats.dll",
            "ICSharpCode.SharpZipLib.dll",
            "BouncyCastle.Cryptography.dll",
            "Enums.NET.dll",
            "ExtendedNumerics.BigDecimal.dll",
            "MathNet.Numerics.dll",
            "Microsoft.Bcl.Cryptography.dll",
            "Microsoft.IO.RecyclableMemoryStream.dll",
            "System.Buffers.dll",
            "System.Formats.Asn1.dll",
            "System.Memory.dll",
            "System.Numerics.Vectors.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Security.Cryptography.Xml.dll",
            "System.Text.Encoding.CodePages.dll",
            "System.Threading.Tasks.Extensions.dll",
            "ZString.dll"
        };

        public static readonly string[] ComponentAssemblies =
        {
            "CDBox.Common.dll",
            "CDBox.Wastewater.dll",
            "CDBox.RealEstate.dll"
        };

        public const string ComponentManifestFileName = "components.json";
        public const string WebView2LoaderRelativePath = "runtimes\\win-x64\\native\\WebView2Loader.dll";
    }
}
