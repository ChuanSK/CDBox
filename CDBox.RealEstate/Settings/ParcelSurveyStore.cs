using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Settings
{
    public sealed class ParcelSurveyStore
    {
        private static readonly object Gate = new object();
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private readonly string _path;

        public ParcelSurveyStore() : this(DefaultPath()) { }

        public ParcelSurveyStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("宗地调查数据路径不能为空。", "path");
            _path = path;
        }

        public ParcelSurveyRepositoryState Load()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(_path)) return Normalize(
                        new ParcelSurveyRepositoryState());
                    return Normalize(Serializer.Deserialize<ParcelSurveyRepositoryState>(
                        File.ReadAllText(_path)));
                }
                catch
                {
                    return Normalize(new ParcelSurveyRepositoryState());
                }
            }
        }

        public ParcelSurveyRecord Current()
        {
            ParcelSurveyRepositoryState state = Load();
            ParcelSurveyRecord current = state.Records.FirstOrDefault(x =>
                string.Equals(x.Id, state.CurrentRecordId,
                    StringComparison.OrdinalIgnoreCase));
            if (current != null) return current;
            current = CreateRecord(state.ProjectDefaults);
            state.Records.Add(current);
            state.CurrentRecordId = current.Id;
            SaveState(state);
            return current;
        }

        public ParcelSurveyRecord Save(ParcelSurveyRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            record.Normalize();
            record.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                int index = state.Records.FindIndex(x => string.Equals(x.Id,
                    record.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0) state.Records.Add(record);
                else state.Records[index] = record;
                state.CurrentRecordId = record.Id;
                CaptureProjectDefaults(state, record);
                SaveState(state);
            }
            return record;
        }

        public ParcelSurveyRecord CreateNew()
        {
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord record = CreateRecord(state.ProjectDefaults);
                state.Records.Add(record);
                state.CurrentRecordId = record.Id;
                SaveState(state);
                return record;
            }
        }

        private void SaveState(ParcelSurveyRepositoryState state)
        {
            state = Normalize(state);
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string temporary = _path + ".tmp";
            try
            {
                File.WriteAllText(temporary, Serializer.Serialize(state));
                File.Copy(temporary, _path, true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        private static ParcelSurveyRepositoryState Normalize(
            ParcelSurveyRepositoryState state)
        {
            state = state ?? new ParcelSurveyRepositoryState();
            state.ProjectDefaults = state.ProjectDefaults ??
                new Dictionary<string, ParcelSurveyFieldValue>(
                    StringComparer.OrdinalIgnoreCase);
            state.Records = state.Records ?? new List<ParcelSurveyRecord>();
            state.Records.RemoveAll(x => x == null);
            foreach (ParcelSurveyRecord record in state.Records) record.Normalize();
            foreach (ParcelSurveyFieldValue value in state.ProjectDefaults.Values)
                if (value != null) value.Normalize();
            return state;
        }

        private static ParcelSurveyRecord CreateRecord(
            IDictionary<string, ParcelSurveyFieldValue> defaults)
        {
            var record = new ParcelSurveyRecord();
            record.Normalize();
            foreach (ParcelSurveyFieldDefinition definition in
                ParcelSurveyFieldCatalog.Fields)
            {
                ParcelSurveyFieldValue source;
                if (!definition.SupportsProjectDefault || defaults == null
                    || !defaults.TryGetValue(definition.Key, out source)
                    || source == null) continue;
                record.Fields[definition.Key] = Clone(source);
                record.Fields[definition.Key].Status = ParcelFieldStatus.Default;
            }
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            SetSystemDate(record, "project.formDate", today);
            record.Normalize();
            return record;
        }

        private static void SetSystemDate(ParcelSurveyRecord record,
            string key, string today)
        {
            ParcelSurveyFieldValue value = record.Field(key);
            if (value != null && string.IsNullOrWhiteSpace(value.TextValue))
            {
                value.TextValue = today;
                value.Status = ParcelFieldStatus.Automatic;
                value.Confirmed = true;
            }
        }

        private static void CaptureProjectDefaults(
            ParcelSurveyRepositoryState state, ParcelSurveyRecord record)
        {
            foreach (ParcelSurveyFieldDefinition definition in
                ParcelSurveyFieldCatalog.Fields)
            {
                if (!definition.SupportsProjectDefault) continue;
                ParcelSurveyFieldValue value;
                if (!record.Fields.TryGetValue(definition.Key, out value)
                    || value == null || value.Status != ParcelFieldStatus.Default
                    || !value.HasValue(definition.Kind)) continue;
                state.ProjectDefaults[definition.Key] = Clone(value);
            }
        }

        private static ParcelSurveyFieldValue Clone(ParcelSurveyFieldValue source)
        {
            return Serializer.Deserialize<ParcelSurveyFieldValue>(
                Serializer.Serialize(source)) ?? new ParcelSurveyFieldValue();
        }

        private static string DefaultPath()
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "CDBox", "RealEstate",
                "parcel-survey-data.json");
        }
    }
}
