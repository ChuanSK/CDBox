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
            return Current(documentId, documentName,
                string.IsNullOrWhiteSpace(regionId) ? "whole" : "region",
                regionId, regionName, string.Empty, string.Empty);
        }

        public ParcelSurveyRecord Current(string documentId,
            string documentName, string scopeType, string regionId,
            string regionName, string parcelId, string parcelName)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return Current();
            string selectedRegion = NormalizeKey(regionId);
            string selectedParcel = NormalizeKey(parcelId);
            string selectedType = NormalizeScopeType(scopeType,
                selectedRegion, selectedParcel);
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord current = state.Records.FirstOrDefault(x =>
                    Same(x.DocumentId, docId) && Same(x.ScopeType, selectedType)
                    && (selectedType == "whole"
                        || (selectedType == "region" && Same(x.RegionId,
                            selectedRegion))
                        || (selectedType == "parcel" && Same(x.ParcelId,
                            selectedParcel))));
                if (current == null && selectedType == "whole")
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
                    regionName, selectedParcel, parcelName, selectedType);
                state.CurrentRecordId = current.Id;
                ApplyCurrentScope(state, docId, selectedType,
                    selectedRegion, selectedParcel);
                SaveState(state);
                return current;
            }
        }

        public IList<ParcelSurveyParcelInfo> GetParcels(string documentId)
        {
            string docId = NormalizeKey(documentId);
            ParcelSurveyRepositoryState state = Load();
            return state.Records.Where(x => Same(x.DocumentId, docId)
                    && (Same(x.ScopeType, "parcel")
                        || Same(x.ScopeType, "region")))
                .Select(x => new ParcelSurveyParcelInfo
                {
                    RecordId = x.Id,
                    ScopeType = x.ScopeType,
                    BindingKey = x.ScopeType + ":" + (x.ScopeType == "region"
                        ? x.RegionId : x.ParcelId),
                    ParcelId = x.ParcelId,
                    ParcelName = x.ScopeType == "region"
                        ? First(x.ParcelName, x.RegionName)
                        : First(x.ParcelName,
                            x.Field("rights.ownerName").TextValue),
                    RegionId = x.RegionId,
                    OwnerName = x.Field("rights.ownerName").TextValue,
                    SourceObjectHandle = x.Boundary.SourceObjectHandle,
                    BoundaryValid = x.Boundary.ParcelBoundaryClosed
                        && x.Boundary.Points.Count >= 3
                }).OrderBy(x => x.ParcelName,
                    StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public IList<ParcelSurveyRecord> GetBoundRecords(string documentId)
        {
            string docId = NormalizeKey(documentId);
            ParcelSurveyRepositoryState state = Load();
            return state.Records.Where(x => Same(x.DocumentId, docId)
                    && (Same(x.ScopeType, "parcel")
                        || Same(x.ScopeType, "region")))
                .ToList();
        }

        public ParcelSurveyRecord GetRecord(string recordId)
        {
            string id = NormalizeKey(recordId);
            if (id.Length == 0) return null;
            return Load().Records.FirstOrDefault(x => Same(x.Id, id));
        }

        public ParcelSurveyRecord SelectRecord(string documentId,
            string recordId)
        {
            string docId = NormalizeKey(documentId);
            string id = NormalizeKey(recordId);
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord record = state.Records.FirstOrDefault(x =>
                    Same(x.Id, id) && Same(x.DocumentId, docId)
                    && (Same(x.ScopeType, "parcel")
                        || Same(x.ScopeType, "region")));
                if (record == null) return null;
                state.CurrentRecordId = record.Id;
                ApplyCurrentScope(state, docId, record.ScopeType,
                    record.RegionId, record.ParcelId);
                SaveState(state);
                return record;
            }
        }

        public void RenameParcelInfo(string documentId, string recordId,
            string parcelName)
        {
            string docId = NormalizeKey(documentId);
            string id = NormalizeKey(recordId);
            string name = NormalizeKey(parcelName);
            if (docId.Length == 0 || id.Length == 0 || name.Length == 0)
                return;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord record = state.Records.FirstOrDefault(x =>
                    Same(x.Id, id) && Same(x.DocumentId, docId));
                if (record == null) return;
                EnsureUniqueParcelName(state, docId, name, record.Id);
                record.ParcelName = name;
                if (Same(record.ScopeType, "region"))
                    record.RegionName = name;
                SaveState(state);
            }
        }

        public ParcelSurveyRecord DeleteParcelInfo(string documentId,
            string recordId)
        {
            string docId = NormalizeKey(documentId);
            string id = NormalizeKey(recordId);
            if (docId.Length == 0 || id.Length == 0) return null;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                ParcelSurveyRecord record = state.Records.FirstOrDefault(x =>
                    Same(x.Id, id) && Same(x.DocumentId, docId));
                if (record == null) return null;
                bool deletingCurrent = Same(state.CurrentRecordId, record.Id)
                    || IsCurrentScope(state, docId, record);
                state.Records.Remove(record);
                if (deletingCurrent)
                {
                    ParcelSurveyRecord next = state.Records.FirstOrDefault(x =>
                        Same(x.DocumentId, docId)
                        && (Same(x.ScopeType, "parcel")
                            || Same(x.ScopeType, "region")));
                    if (next == null)
                    {
                        state.CurrentRecordId = string.Empty;
                        ApplyCurrentScope(state, docId, "whole",
                            string.Empty, string.Empty);
                    }
                    else
                    {
                        state.CurrentRecordId = next.Id;
                        ApplyCurrentScope(state, docId, next.ScopeType,
                            next.RegionId, next.ParcelId);
                    }
                }
                SaveState(state);
                return record;
            }
        }

        public ParcelSurveyRecord CreateOrSelectParcel(string documentId,
            string documentName, string ownerName, string sourceObjectHandle)
        {
            string docId = NormalizeKey(documentId);
            string owner = NormalizeKey(ownerName);
            if (docId.Length == 0)
                throw new ArgumentException("宗地必须关联有效图纸。",
                    "documentId");
            if (owner.Length == 0)
                throw new ArgumentException("权利人姓名不能为空。",
                    "ownerName");
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                string handle = NormalizeKey(sourceObjectHandle);
                EnsureUniqueParcelName(state, docId, owner, string.Empty);

                // A CAD boundary is a binding source, not the identity of a
                // parcel survey record.  Several parcels may intentionally
                // reuse the same closed polyline, so every explicit create
                // operation receives its own record and parcel id.
                ParcelSurveyRecord record = CreateRecord(
                    state.ProjectDefaults);
                record.ParcelId = Guid.NewGuid().ToString("N");
                ApplyScope(record, docId, documentName, string.Empty,
                    string.Empty, record.ParcelId, owner, "parcel");
                record.Boundary.SourceObjectHandle = handle;
                state.Records.Add(record);
                ParcelSurveyFieldValue ownerField = record.Field(
                    "rights.ownerName");
                ownerField.TextValue = owner;
                ownerField.Status = ParcelFieldStatus.Manual;
                ownerField.Confirmed = true;
                state.CurrentRecordId = record.Id;
                ApplyCurrentScope(state, docId, record.ScopeType,
                    record.RegionId, record.ParcelId);
                SaveState(state);
                return record;
            }
        }

        public string GetCurrentScopeType(string documentId)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return "whole";
            ParcelSurveyRepositoryState state = Load();
            string scopeType;
            return state.CurrentScopeTypeByDocument.TryGetValue(docId,
                out scopeType) ? NormalizeScopeType(scopeType,
                    GetValue(state.CurrentRegionByDocument, docId),
                    GetValue(state.CurrentParcelByDocument, docId)) : "whole";
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

        public string GetCurrentParcelId(string documentId)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return string.Empty;
            ParcelSurveyRepositoryState state = Load();
            string parcelId;
            return state.CurrentParcelByDocument.TryGetValue(docId,
                out parcelId) ? NormalizeKey(parcelId) : string.Empty;
        }

        public void SelectScope(string documentId, string regionId)
        {
            SelectScope(documentId, string.IsNullOrWhiteSpace(regionId)
                ? "whole" : "region", regionId);
        }

        public void SelectScope(string documentId, string scopeType,
            string scopeId)
        {
            string docId = NormalizeKey(documentId);
            if (docId.Length == 0) return;
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                string type = NormalizeScopeType(scopeType,
                    string.Equals(scopeType, "region",
                        StringComparison.OrdinalIgnoreCase) ? scopeId : null,
                    string.Equals(scopeType, "parcel",
                        StringComparison.OrdinalIgnoreCase) ? scopeId : null);
                ApplyCurrentScope(state, docId, type,
                    type == "region" ? scopeId : string.Empty,
                    type == "parcel" ? scopeId : string.Empty);
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
                    ApplyCurrentScope(state, docId, "whole",
                        string.Empty, string.Empty);
                SaveState(state);
            }
        }

        public ParcelSurveyRecord Save(ParcelSurveyRecord record)
        {
            return Save(record, true);
        }

        public ParcelSurveyRecord SavePreservingCurrentScope(
            ParcelSurveyRecord record)
        {
            return Save(record, false, false);
        }

        private ParcelSurveyRecord Save(ParcelSurveyRecord record,
            bool selectRecord)
        {
            return Save(record, selectRecord, true);
        }

        private ParcelSurveyRecord Save(ParcelSurveyRecord record,
            bool selectRecord, bool allowInsert)
        {
            if (record == null) throw new ArgumentNullException("record");
            record.Normalize();
            record.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            lock (Gate)
            {
                ParcelSurveyRepositoryState state = Load();
                int index = state.Records.FindIndex(x => string.Equals(x.Id,
                    record.Id, StringComparison.OrdinalIgnoreCase));
                // Background/view-state saves must never recreate a record
                // that the user has just deleted.  This also closes the race
                // between the old editor page's scope poll and page refresh.
                if (index < 0 && !allowInsert) return null;
                if (index < 0) state.Records.Add(record);
                else state.Records[index] = record;
                if (selectRecord) state.CurrentRecordId = record.Id;
                if (selectRecord
                    && !string.IsNullOrWhiteSpace(record.DocumentId))
                    ApplyCurrentScope(state, record.DocumentId,
                        record.ScopeType, record.RegionId, record.ParcelId);
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
            state.CurrentParcelByDocument = state.CurrentParcelByDocument ??
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            state.CurrentParcelByDocument = new Dictionary<string, string>(
                state.CurrentParcelByDocument, StringComparer.OrdinalIgnoreCase);
            state.CurrentScopeTypeByDocument = state.CurrentScopeTypeByDocument
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            state.CurrentScopeTypeByDocument = new Dictionary<string, string>(
                state.CurrentScopeTypeByDocument,
                StringComparer.OrdinalIgnoreCase);
            foreach (string documentId in state.CurrentRegionByDocument.Keys
                .Concat(state.CurrentParcelByDocument.Keys).Distinct(
                    StringComparer.OrdinalIgnoreCase).ToList())
            {
                string type;
                if (!state.CurrentScopeTypeByDocument.TryGetValue(documentId,
                    out type))
                    state.CurrentScopeTypeByDocument[documentId] =
                        !string.IsNullOrWhiteSpace(GetValue(
                            state.CurrentParcelByDocument, documentId))
                            ? "parcel"
                            : !string.IsNullOrWhiteSpace(GetValue(
                                state.CurrentRegionByDocument, documentId))
                                ? "region" : "whole";
            }
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
            string regionName, string parcelId, string parcelName,
            string scopeType)
        {
            record.DocumentId = documentId;
            record.DocumentName = (documentName ?? string.Empty).Trim();
            record.ScopeType = NormalizeScopeType(scopeType, regionId,
                parcelId);
            record.RegionId = record.ScopeType == "region"
                ? regionId : string.Empty;
            record.RegionName = record.ScopeType == "region"
                ? (regionName ?? string.Empty).Trim() : string.Empty;
            record.ParcelId = record.ScopeType == "parcel"
                ? parcelId : string.Empty;
            record.ParcelName = record.ScopeType == "parcel"
                ? (parcelName ?? string.Empty).Trim() : string.Empty;
            record.Normalize();
        }

        private static void ApplyCurrentScope(ParcelSurveyRepositoryState state,
            string documentId, string scopeType, string regionId,
            string parcelId)
        {
            string docId = NormalizeKey(documentId);
            string type = NormalizeScopeType(scopeType, regionId, parcelId);
            state.CurrentScopeTypeByDocument[docId] = type;
            state.CurrentRegionByDocument[docId] = type == "region"
                ? NormalizeKey(regionId) : string.Empty;
            state.CurrentParcelByDocument[docId] = type == "parcel"
                ? NormalizeKey(parcelId) : string.Empty;
        }

        private static bool IsCurrentScope(ParcelSurveyRepositoryState state,
            string documentId, ParcelSurveyRecord record)
        {
            if (state == null || record == null) return false;
            string scopeType = GetValue(state.CurrentScopeTypeByDocument,
                documentId);
            if (!Same(scopeType, record.ScopeType)) return false;
            if (Same(record.ScopeType, "parcel"))
                return Same(GetValue(state.CurrentParcelByDocument,
                    documentId), record.ParcelId);
            if (Same(record.ScopeType, "region"))
                return Same(GetValue(state.CurrentRegionByDocument,
                    documentId), record.RegionId);
            return false;
        }

        private static string NormalizeScopeType(string scopeType,
            string regionId, string parcelId)
        {
            if (string.Equals(scopeType, "parcel",
                StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parcelId)) return "parcel";
            if (string.Equals(scopeType, "region",
                StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(regionId)) return "region";
            return "whole";
        }

        private static string GetValue(IDictionary<string, string> values,
            string key)
        {
            if (values == null) return string.Empty;
            string value;
            return values.TryGetValue(key ?? string.Empty, out value)
                ? value ?? string.Empty : string.Empty;
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback ?? string.Empty : value;
        }

        private static void EnsureUniqueParcelName(
            ParcelSurveyRepositoryState state, string documentId,
            string parcelName, string exceptRecordId)
        {
            string name = NormalizeKey(parcelName);
            if (name.Length == 0) return;
            ParcelSurveyRecord duplicate = (state == null
                    ? Enumerable.Empty<ParcelSurveyRecord>()
                    : state.Records ?? new List<ParcelSurveyRecord>())
                .FirstOrDefault(x => x != null
                    && Same(x.DocumentId, documentId)
                    && !Same(x.Id, exceptRecordId)
                    && (Same(x.ScopeType, "parcel")
                        || Same(x.ScopeType, "region"))
                    && Same(ParcelDisplayName(x), name));
            if (duplicate != null)
                throw new InvalidOperationException(
                    "当前图纸已存在同名宗地“" + name + "”，请使用其他宗地名。");
        }

        private static string ParcelDisplayName(ParcelSurveyRecord record)
        {
            if (record == null) return string.Empty;
            if (Same(record.ScopeType, "region"))
                return First(record.ParcelName, record.RegionName);
            ParcelSurveyFieldValue owner = record.Field("rights.ownerName");
            return First(record.ParcelName,
                owner == null ? string.Empty : owner.TextValue);
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
