using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDBox.Setup
{
    internal static class CadDetectionService
    {
        private const string AutoCadRegistryPath =
            @"SOFTWARE\Autodesk\AutoCAD";

        private static readonly IReadOnlyDictionary<string, int>
            KnownReleaseYears = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "R16.0", 2004 }, { "R16.1", 2005 },
                { "R16.2", 2006 }, { "R17.0", 2007 },
                { "R17.1", 2008 }, { "R17.2", 2009 },
                { "R18.0", 2010 }, { "R18.1", 2011 },
                { "R18.2", 2012 }, { "R19.0", 2013 },
                { "R19.1", 2014 }, { "R20.0", 2015 },
                { "R20.1", 2016 }, { "R21.0", 2017 },
                { "R22.0", 2018 }, { "R23.0", 2019 },
                { "R23.1", 2020 }, { "R24.0", 2021 },
                { "R24.1", 2022 }, { "R24.2", 2023 },
                { "R24.3", 2024 }, { "R25.0", 2025 },
                { "R25.1", 2026 }
            };

        public static IReadOnlyList<CadInstallation>
            DetectSupportedVersions()
        {
            var detected = new List<CadInstallation>();
            foreach (RegistryView view in RegistryViews())
                DetectFromLocalMachine(view, detected);

            return detected.Where(x => x != null && x.IsDetected)
                .GroupBy(x => NormalizePath(x.AcadExecutablePath),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(
                        IsCurrentUserProduct)
                    .ThenBy(x => x.ProductKey,
                        StringComparer.OrdinalIgnoreCase).First())
                .OrderBy(x => x.Version.Year <= 0
                    ? int.MaxValue : x.Version.Year)
                .ThenBy(x => x.Version.ReleaseKey,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void DetectFromLocalMachine(RegistryView view,
            ICollection<CadInstallation> detected)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine, view))
                using (RegistryKey autoCad = root.OpenSubKey(
                    AutoCadRegistryPath))
                {
                    if (autoCad == null) return;
                    foreach (string releaseKey in autoCad.GetSubKeyNames())
                    {
                        if (!IsReleaseKey(releaseKey)) continue;
                        using (RegistryKey release = autoCad.OpenSubKey(
                            releaseKey))
                        {
                            if (release == null) continue;
                            foreach (string productKey in
                                release.GetSubKeyNames())
                            {
                                using (RegistryKey product =
                                    release.OpenSubKey(productKey))
                                {
                                    string installDirectory =
                                        ReadCadDirectory(product);
                                    if (!IsCadDirectory(installDirectory))
                                        continue;
                                    string productName = Convert.ToString(
                                        product == null ? null
                                            : product.GetValue(
                                                "ProductName"))
                                        ?? string.Empty;
                                    int year = InferYear(productName,
                                        installDirectory, releaseKey);
                                    string displayName = BuildDisplayName(
                                        productName, year, releaseKey);
                                    detected.Add(new CadInstallation(
                                        new CadVersionDefinition(year,
                                            releaseKey, displayName),
                                        installDirectory, productKey));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 继续检查其他注册表视图；单个损坏产品项不应阻断安装器。
            }
        }

        internal static int InferYear(string productName,
            string installDirectory, string releaseKey)
        {
            Match match = Regex.Match((productName ?? string.Empty) + " "
                + (installDirectory ?? string.Empty),
                @"(?<!\d)(20\d{2})(?!\d)", RegexOptions.CultureInvariant);
            int year;
            if (match.Success && int.TryParse(match.Groups[1].Value,
                    out year)) return year;
            return KnownReleaseYears.TryGetValue(
                (releaseKey ?? string.Empty).Trim(), out year)
                ? year : 0;
        }

        internal static string BuildDisplayName(string productName,
            int year, string releaseKey)
        {
            if (year > 0) return "AutoCAD " + year;
            string name = (productName ?? string.Empty).Trim();
            if (name.Length > 0) return name;
            return "AutoCAD " + (releaseKey ?? string.Empty).Trim();
        }

        private static bool IsCurrentUserProduct(
            CadInstallation installation)
        {
            if (installation == null) return false;
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(
                    RegistryHive.CurrentUser, RegistryView.Default))
                using (RegistryKey release = root.OpenSubKey(
                    @"Software\Autodesk\AutoCAD\"
                    + installation.Version.ReleaseKey))
                {
                    string current = Convert.ToString(release == null
                        ? null : release.GetValue("CurVer")) ?? string.Empty;
                    return string.Equals(current.Trim(),
                        installation.ProductKey,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ReadCadDirectory(RegistryKey key)
        {
            if (key == null) return string.Empty;
            foreach (string valueName in new[] { "AcadLocation",
                "InstallLocation", "GlobUPILocation" })
            {
                string value = Convert.ToString(key.GetValue(valueName))
                    ?? string.Empty;
                value = Environment.ExpandEnvironmentVariables(value.Trim()
                    .Trim('"'));
                if (value.Length == 0) continue;
                if (string.Equals(Path.GetFileName(value.TrimEnd(
                        Path.DirectorySeparatorChar)), "GlobUPI",
                    StringComparison.OrdinalIgnoreCase))
                    value = Path.GetDirectoryName(value.TrimEnd(
                        Path.DirectorySeparatorChar)) ?? value;
                if (IsCadDirectory(value)) return Path.GetFullPath(value);
            }
            return string.Empty;
        }

        private static bool IsCadDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { return File.Exists(Path.Combine(path, "acad.exe")); }
            catch { return false; }
        }

        private static bool IsReleaseKey(string value)
        {
            return Regex.IsMatch((value ?? string.Empty).Trim(),
                @"^R\d+(?:\.\d+)?$", RegexOptions.IgnoreCase
                    | RegexOptions.CultureInvariant);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path ?? string.Empty).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            }
            catch { return (path ?? string.Empty).Trim(); }
        }

        private static IEnumerable<RegistryView> RegistryViews()
        {
            yield return RegistryView.Registry64;
            yield return RegistryView.Registry32;
        }
    }
}
