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

        public ParcelSurveyRecord Current(string documentId,
            string documentName, string regionId, string regionName)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return Current();
            string selectedRegion = NormalizeKey(regionId);
            string scopeType = selectedRegion.Length == 0 ? "whole" : "region";
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord current = state.Records.FirstOrDefault(x =>
                    Same(x.DocumentId, docId) && Same(x.ScopeType, scopeType)
                    && (scopeType == "whole" || Same(x.RegionId,
                        selectedRegion)));
                if (current == null && scopeType == "whole")
                {
                    current = state.Records.FirstOrDefault(x =>
                        string.IsNullOrWhiteSpace(x.DocumentId)
                        && Same(x.Id, state.CurrentRecordId));
                }
                if (current == null)
                {
                    current = CreateRecord(state.ProjectDefaults);
                    state.Records.Add(current);
                }
                ApplyScope(current, docId, documentName, selectedRegion,
                    regionName);
                state.CurrentRecordId = current.Id;
                state.CurrentRegionByDocument[docId] = selectedRegion;
                SaveState(state);
                return current;
            }
        }

        public string GetCurrentRegionId(string documentId)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return string.Empty;
            ParcelSurveyRepositoryState state = Load();
            string regionId;
            return state.CurrentRegionByDocument.TryGetValue(docId,
                out regionId) ? NormalizeKey(regionId) : string.Empty;
        }

        public void SelectScope(string documentId, string regionId)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                state.CurrentRegionByDocument[docId] = NormalizeKey(regionId);
                SaveState(state);
            }
        }

        public void RenameRegion(string documentId, string regionId,
            string regionName)
        {
            string docId = NormalizeKey(documentId);
            string target = NormalizeKey(regionId);
            if (docId.Length == 0 || target.Length == 0) return;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                foreach (ParcelSurveyRecord record in state.Records.Where(x =>
                    Same(x.DocumentId, docId) && Same(x.RegionId, target)))
                    record.RegionName = (regionName ?? string.Empty).Trim();
                SaveState(state);
            }
        }

        public void DeleteRegion(string documentId, string regionId)
        {
            string docId = NormalizeKey(documentId);
            string target = NormalizeKey(regionId);
            if (docId.Length == 0 || target.Length == 0) return;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                state.Records.RemoveAll(x => Same(x.DocumentId, docId)
                    && Same(x.RegionId, target));
                string selected;
                if (state.CurrentRegionByDocument.TryGetValue(docId,
                    out selected) && Same(selected, target))
                    state.CurrentRegionByDocument[docId] = string.Empty;
                SaveState(state);
            }
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
                if (!string.IsNullOrWhiteSpace(record.DocumentId))
                    state.CurrentRegionByDocument[record.DocumentId] =
                        record.ScopeType == "region"
                            ? record.RegionId ?? string.Empty : string.Empty;
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
            state.ProjectDefaults = new Dictionary<string,
                ParcelSurveyFieldValue>(state.ProjectDefaults,
                    StringComparer.OrdinalIgnoreCase);
            state.CurrentRegionByDocument = state.CurrentRegionByDocument ??
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            state.CurrentRegionByDocument = new Dictionary<string, string>(
                state.CurrentRegionByDocument, StringComparer.OrdinalIgnoreCase);
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
                if (defaults == null
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

        private static void ApplyScope(ParcelSurveyRecord record,
            string documentId, string documentName, string regionId,
            string regionName)
        {
            record.DocumentId = documentId;
            record.DocumentName = (documentName ?? string.Empty).Trim();
            record.ScopeType = string.IsNullOrWhiteSpace(regionId)
                ? "whole" : "region";
            record.RegionId = record.ScopeType == "region"
                ? regionId : string.Empty;
            record.RegionName = record.ScopeType == "region"
                ? (regionName ?? string.Empty).Trim() : string.Empty;
            record.Normalize();
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(NormalizeKey(left), NormalizeKey(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string DefaultPath()
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "CDBox", "RealEstate",
                "parcel-survey-data.json");
        }
    }
}
