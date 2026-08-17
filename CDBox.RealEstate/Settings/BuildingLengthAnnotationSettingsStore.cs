using System;
using System.IO;
using System.Xml.Serialization;

namespace CDBox.RealEstate.Settings
{
    internal static class BuildingLengthAnnotationSettingsStore
    {
        private static readonly object Gate = new object();
        private static readonly XmlSerializer Serializer =
            new XmlSerializer(typeof(BuildingLengthAnnotationSettings));

        public static BuildingLengthAnnotationSettings Load()
        {
            lock (Gate)
            {
                try
                {
                    string path = SettingsPath();
                    if (!File.Exists(path))
                        return Normalize(new BuildingLengthAnnotationSettings());
                    using (FileStream stream = File.OpenRead(path))
                        return Normalize(Serializer.Deserialize(stream)
                            as BuildingLengthAnnotationSettings);
                }
                catch
                {
                    return Normalize(new BuildingLengthAnnotationSettings());
                }
            }
        }

        public static void Save(BuildingLengthAnnotationSettings settings)
        {
            settings = Normalize(settings);
            lock (Gate)
            {
                string path = SettingsPath();
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                try
                {
                    using (FileStream stream = File.Create(temporary))
                        Serializer.Serialize(stream, settings);
                    File.Copy(temporary, path, true);
                }
                finally
                {
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch { }
                }
            }
        }

        private static BuildingLengthAnnotationSettings Normalize(
            BuildingLengthAnnotationSettings settings)
        {
            settings = settings ?? new BuildingLengthAnnotationSettings();
            settings.Normalize();
            return settings;
        }

        private static string SettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "CDBox",
                "RealEstate", "building-length-annotation.settings.xml");
        }
    }
}
