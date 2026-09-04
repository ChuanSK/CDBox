using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 污水业务拥有的工程量属性 Xrecord 存储实现。保留原有主记录、
    /// 命名对象字典备份和键值格式，旧图纸无需迁移即可继续读取。
    /// </summary>
    public sealed class WastewaterQuantityAttributeCadStore
        : IQuantityAttributeCadStore
    {
        public const string XrecordName = "CDBoxQuantityPipeAttributes";
        private const string IndexDictionaryName =
            "CDBoxQuantityAttributeIndex";

        public bool HasAttributes(object entity, object transaction)
        {
            return GetRecord(AsEntity(entity), AsTransaction(transaction))
                != null;
        }

        public bool HasAttributeKey(object entity, object transaction,
            string keyName)
        {
            Entity target = AsEntity(entity);
            Transaction tr = AsTransaction(transaction);
            if (target == null || tr == null
                || string.IsNullOrWhiteSpace(keyName)) return false;
            try
            {
                Xrecord record = GetRecord(target, tr);
                if (record == null || record.Data == null) return false;
                foreach (TypedValue value in record.Data)
                {
                    string text = value.Value == null
                        ? string.Empty : value.Value.ToString();
                    int separator = text.IndexOf('=');
                    if (separator <= 0) continue;
                    if (string.Equals(text.Substring(0, separator).Trim(),
                        keyName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        public IReadOnlyCollection<string> ReadAttributeKeys(object entity,
            object transaction)
        {
            var keys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            Entity target = AsEntity(entity);
            Transaction tr = AsTransaction(transaction);
            if (target == null || tr == null) return keys;
            try
            {
                Xrecord record = GetRecord(target, tr);
                if (record == null || record.Data == null) return keys;
                foreach (TypedValue value in record.Data)
                {
                    string text = value.Value == null
                        ? string.Empty : value.Value.ToString();
                    int separator = text.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = text.Substring(0, separator).Trim();
                    if (key.Length > 0) keys.Add(key);
                }
            }
            catch { }
            return keys;
        }

        public QuantityPipeAttributes ReadAttributes(object entity,
            object transaction)
        {
            Entity target = AsEntity(entity);
            Transaction tr = AsTransaction(transaction);
            QuantityPipeAttributes attributes =
                QuantityPipeAttributes.Default.Clone();
            if (target == null || tr == null) return attributes;
            try
            {
                Xrecord record = GetRecord(target, tr);
                if (record == null || record.Data == null) return attributes;
                bool hasAnnotationFlag = false;
                foreach (TypedValue value in record.Data)
                {
                    string text = value.Value == null
                        ? string.Empty : value.Value.ToString();
                    int separator = text.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = text.Substring(0, separator).Trim();
                    string valueText = text.Substring(separator + 1).Trim();
                    if (string.Equals(key,
                        "DrawLengthWidthHeightAnnotation",
                        StringComparison.OrdinalIgnoreCase))
                        hasAnnotationFlag = true;
                    Apply(attributes, key, valueText);
                }
                if (!hasAnnotationFlag)
                    attributes.DrawLengthWidthHeightAnnotation =
                        QuantityPipeAttributes.IsMainPipeKind(
                            attributes.ObjectKind);
            }
            catch { return attributes; }
            attributes.BackfillStructure = QuantityPipeAttributes
                .NormalizeStructureLayerText(attributes.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(attributes);
            return attributes;
        }

        public void WriteAttributes(object entity, object transaction,
            QuantityPipeAttributes attributes)
        {
            Entity target = AsEntity(entity);
            Transaction tr = AsTransaction(transaction);
            if (target == null || tr == null || attributes == null) return;
            if (!target.IsWriteEnabled) target.UpgradeOpen();
            if (target.ExtensionDictionary.IsNull)
                target.CreateExtensionDictionary();
            var dictionary = tr.GetObject(target.ExtensionDictionary,
                OpenMode.ForWrite) as DBDictionary;
            if (dictionary == null) return;
            Xrecord record;
            if (dictionary.Contains(XrecordName))
                record = tr.GetObject(dictionary.GetAt(XrecordName),
                    OpenMode.ForWrite, false) as Xrecord;
            else
            {
                record = new Xrecord();
                dictionary.SetAt(XrecordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record == null) return;

            attributes.BackfillStructure = QuantityPipeAttributes
                .NormalizeStructureLayerText(attributes.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(attributes);
            record.Data = BuildData(attributes);
            WriteBackup(target, tr, record.Data);
        }

        public bool RemoveAttributes(object entity, object transaction)
        {
            Entity target = AsEntity(entity);
            Transaction tr = AsTransaction(transaction);
            if (target == null || tr == null) return false;
            bool removed = false;
            try
            {
                if (!target.ExtensionDictionary.IsNull)
                {
                    var dictionary = tr.GetObject(target.ExtensionDictionary,
                        OpenMode.ForRead, false) as DBDictionary;
                    if (dictionary != null && dictionary.Contains(XrecordName))
                    {
                        if (!dictionary.IsWriteEnabled)
                            dictionary.UpgradeOpen();
                        ObjectId recordId = dictionary.GetAt(XrecordName);
                        dictionary.Remove(XrecordName);
                        Erase(tr, recordId);
                        removed = true;
                    }
                }
                DBDictionary index = GetIndex(target.Database, tr, false);
                string key = target.Handle.ToString();
                if (index != null && index.Contains(key))
                {
                    if (!index.IsWriteEnabled) index.UpgradeOpen();
                    ObjectId backupId = index.GetAt(key);
                    index.Remove(key);
                    Erase(tr, backupId);
                    removed = true;
                }
            }
            catch { }
            return removed;
        }

        public bool TryReadSavedAttributes(object database,
            object transaction, object objectId,
            out QuantityPipeAttributes attributes)
        {
            attributes = null;
            Database db = database as Database;
            Transaction tr = AsTransaction(transaction);
            if (db == null || tr == null || !(objectId is ObjectId))
                return false;
            ObjectId id = (ObjectId)objectId;
            if (id.IsNull) return false;
            try
            {
                Entity entity = tr.GetObject(id, OpenMode.ForRead, false)
                    as Entity;
                if (entity == null || !HasAttributes(entity, tr))
                    return false;
                attributes = ReadAttributes(entity, tr);
                return attributes != null;
            }
            catch { return false; }
        }

        public IReadOnlyList<string> RepairClonedAttributeObjects(
            object database, object transaction, object cloneMap)
        {
            var changedNodeHandles = new List<string>();
            Database db = database as Database;
            Transaction tr = AsTransaction(transaction);
            var map = cloneMap as IDictionary<ObjectId, ObjectId>;
            if (db == null || tr == null || map == null)
                return changedNodeHandles;
            foreach (KeyValuePair<ObjectId, ObjectId> pair in map)
            {
                if (pair.Value.IsNull) continue;
                Entity clone;
                try
                {
                    clone = tr.GetObject(pair.Value, OpenMode.ForRead, false)
                        as Entity;
                }
                catch { continue; }
                if (clone == null) continue;
                QuantityPipeAttributes attributes = HasAttributes(clone, tr)
                    ? ReadAttributes(clone, tr) : null;
                bool originalInDestination = false;
                try
                {
                    originalInDestination = !pair.Key.IsNull
                        && pair.Key.Database == db;
                }
                catch { }
                if (attributes == null && originalInDestination)
                    try
                    {
                        Entity original = tr.GetObject(pair.Key,
                            OpenMode.ForRead, false) as Entity;
                        if (original != null && HasAttributes(original, tr))
                            attributes = ReadAttributes(original, tr);
                    }
                    catch { }
                if (attributes == null) continue;
                try
                {
                    if (!clone.IsWriteEnabled) clone.UpgradeOpen();
                    WriteAttributes(clone, tr, attributes.Clone());
                }
                catch { continue; }
                if (QuantityPipeAttributes.IsNodeKind(attributes.ObjectKind))
                    changedNodeHandles.Add(clone.Handle.ToString());
            }
            return changedNodeHandles;
        }

        private static Entity AsEntity(object value)
        {
            return value as Entity;
        }

        private static Transaction AsTransaction(object value)
        {
            return value as Transaction;
        }

        private static Xrecord GetRecord(Entity entity, Transaction tr)
        {
            if (entity == null || tr == null) return null;
            try
            {
                if (!entity.ExtensionDictionary.IsNull)
                {
                    var dictionary = tr.GetObject(entity.ExtensionDictionary,
                        OpenMode.ForRead, false) as DBDictionary;
                    if (dictionary != null && dictionary.Contains(XrecordName))
                    {
                        Xrecord primary = tr.GetObject(
                            dictionary.GetAt(XrecordName), OpenMode.ForRead,
                            false) as Xrecord;
                        if (primary != null && primary.Data != null)
                            return primary;
                    }
                }
                DBDictionary index = GetIndex(entity.Database, tr, false);
                string key = entity.Handle.ToString();
                return index != null && index.Contains(key)
                    ? tr.GetObject(index.GetAt(key), OpenMode.ForRead, false)
                        as Xrecord
                    : null;
            }
            catch { return null; }
        }

        private static ResultBuffer BuildData(QuantityPipeAttributes a)
        {
            return new ResultBuffer(
                Pair("SchemaVersion", QuantityPipeAttributes.SchemaVersion),
                Pair("Enabled", a.Enabled), Pair("ObjectKind", a.ObjectKind),
                Pair("IsSpecialObject", a.IsSpecialObject),
                Pair("LayerParentGroup", a.LayerParentGroup),
                Pair("LayerParentClass", a.LayerParentClass),
                Pair("LayerTags", a.LayerTags), Pair("Material", a.Material),
                Pair("Diameter", a.Diameter),
                Pair("UseManualLength", a.UseManualLength),
                Pair("ManualLength", a.ManualLength),
                Pair("DrawLengthWidthHeightAnnotation",
                    a.DrawLengthWidthHeightAnnotation),
                Pair("StartNode", a.StartNode), Pair("EndNode", a.EndNode),
                Pair("StartDepth", a.StartDepth),
                Pair("EndDepth", a.EndDepth),
                Pair("StartInvertElevation", a.StartInvertElevation),
                Pair("EndInvertElevation", a.EndInvertElevation),
                Pair("AverageDepth", a.AverageDepth),
                Pair("TrenchWidth", a.TrenchWidth),
                Pair("RoadThickness", a.RoadThickness),
                Pair("ExcavationType", a.ExcavationType),
                Pair("BackfillType", a.BackfillType),
                Pair("BackfillStructure", a.BackfillStructure),
                Pair("BranchType", a.BranchType),
                Pair("BranchIncludeInCalculation",
                    a.BranchIncludeInCalculation),
                Pair("BranchDepth", a.BranchDepth), Pair("NodeNo", a.NodeNo),
                Pair("WellSpec", a.WellSpec),
                Pair("WellCoverMaterial", a.WellCoverMaterial),
                Pair("WellMaterialType", a.WellMaterialType),
                Pair("WellType", a.WellType),
                Pair("SiltWellDeductDepth500", a.SiltWellDeductDepth500),
                Pair("SiltWellDeductDepth700", a.SiltWellDeductDepth700),
                Pair("GroundElevation", a.GroundElevation),
                Pair("WellDepth", a.WellDepth),
                Pair("ShaftLength", a.ShaftLength),
                Pair("ExcavationLength", a.ExcavationLength),
                Pair("ExcavationWidth", a.ExcavationWidth),
                Pair("CoverPlate", a.CoverPlate),
                Pair("SandCushionThickness", a.SandCushionThickness),
                Pair("GravelCushionThickness", a.GravelCushionThickness),
                Pair("C25RestoreThickness", a.C25RestoreThickness),
                Pair("PipeOuterDiameter", a.PipeOuterDiameter),
                Pair("DeductPipeVolume", a.DeductPipeVolume),
                Pair("Remark", a.Remark), Pair("LastModified",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture)));
        }

        private static void WriteBackup(Entity entity, Transaction tr,
            ResultBuffer data)
        {
            DBDictionary index = GetIndex(entity.Database, tr, true);
            if (index == null || data == null) return;
            string key = entity.Handle.ToString();
            Xrecord record;
            if (index.Contains(key))
                record = tr.GetObject(index.GetAt(key), OpenMode.ForWrite,
                    false) as Xrecord;
            else
            {
                if (!index.IsWriteEnabled) index.UpgradeOpen();
                record = new Xrecord();
                index.SetAt(key, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record != null)
                record.Data = new ResultBuffer(data.AsArray());
        }

        private static DBDictionary GetIndex(Database db, Transaction tr,
            bool create)
        {
            if (db == null || tr == null) return null;
            var nod = tr.GetObject(db.NamedObjectsDictionaryId,
                create ? OpenMode.ForWrite : OpenMode.ForRead, false)
                as DBDictionary;
            if (nod == null) return null;
            if (nod.Contains(IndexDictionaryName))
                return tr.GetObject(nod.GetAt(IndexDictionaryName),
                    create ? OpenMode.ForWrite : OpenMode.ForRead, false)
                    as DBDictionary;
            if (!create) return null;
            var index = new DBDictionary();
            nod.SetAt(IndexDictionaryName, index);
            tr.AddNewlyCreatedDBObject(index, true);
            return index;
        }

        private static void Erase(Transaction tr, ObjectId id)
        {
            try
            {
                DBObject value = tr.GetObject(id, OpenMode.ForWrite, false);
                if (value != null && !value.IsErased) value.Erase();
            }
            catch { }
        }

        private static TypedValue Pair(string key, string value)
        {
            return new TypedValue((int)DxfCode.Text,
                key + "=" + (value ?? string.Empty));
        }

        private static TypedValue Pair(string key, bool value)
        {
            return Pair(key, value ? "true" : "false");
        }

        private static TypedValue Pair(string key, double value)
        {
            return Pair(key, value.ToString("0.########",
                CultureInfo.InvariantCulture));
        }

        private static void Apply(QuantityPipeAttributes a, string key,
            string value)
        {
            if (a == null || string.IsNullOrWhiteSpace(key)) return;
            if (Same(key, "Enabled")) a.Enabled = Bool(value, a.Enabled);
            else if (Same(key, "ObjectKind")) a.ObjectKind = value;
            else if (Same(key, "IsSpecialObject")) a.IsSpecialObject = Bool(value, a.IsSpecialObject);
            else if (Same(key, "LayerParentGroup")) a.LayerParentGroup = value;
            else if (Same(key, "LayerParentClass")) a.LayerParentClass = value;
            else if (Same(key, "LayerTags")) a.LayerTags = value;
            else if (Same(key, "Material")) a.Material = value;
            else if (Same(key, "Diameter")) a.Diameter = value;
            else if (Same(key, "UseManualLength")) a.UseManualLength = Bool(value, a.UseManualLength);
            else if (Same(key, "ManualLength")) a.ManualLength = Number(value, a.ManualLength);
            else if (Same(key, "DrawLengthWidthHeightAnnotation")) a.DrawLengthWidthHeightAnnotation = Bool(value, a.DrawLengthWidthHeightAnnotation);
            else if (Same(key, "StartNode")) a.StartNode = value;
            else if (Same(key, "EndNode")) a.EndNode = value;
            else if (Same(key, "StartDepth")) a.StartDepth = Number(value, a.StartDepth);
            else if (Same(key, "EndDepth")) a.EndDepth = Number(value, a.EndDepth);
            else if (Same(key, "StartInvertElevation")) a.StartInvertElevation = Number(value, a.StartInvertElevation);
            else if (Same(key, "EndInvertElevation")) a.EndInvertElevation = Number(value, a.EndInvertElevation);
            else if (Same(key, "AverageDepth")) a.AverageDepth = Number(value, a.AverageDepth);
            else if (Same(key, "TrenchWidth")) a.TrenchWidth = Number(value, a.TrenchWidth);
            else if (Same(key, "RoadThickness")) a.RoadThickness = Number(value, a.RoadThickness);
            else if (Same(key, "ExcavationType")) a.ExcavationType = value;
            else if (Same(key, "BackfillType")) a.BackfillType = value;
            else if (Same(key, "BackfillStructure")) a.BackfillStructure = value;
            else if (Same(key, "BranchType")) a.BranchType = value;
            else if (Same(key, "BranchIncludeInCalculation")) a.BranchIncludeInCalculation = Bool(value, a.BranchIncludeInCalculation);
            else if (Same(key, "BranchDepth")) a.BranchDepth = Number(value, a.BranchDepth);
            else if (Same(key, "NodeNo")) a.NodeNo = value;
            else if (Same(key, "WellSpec")) a.WellSpec = value;
            else if (Same(key, "WellCoverMaterial")) a.WellCoverMaterial = value;
            else if (Same(key, "WellMaterialType")) a.WellMaterialType = value;
            else if (Same(key, "WellType")) a.WellType = value;
            else if (Same(key, "SiltWellDeductDepth500")) a.SiltWellDeductDepth500 = Number(value, a.SiltWellDeductDepth500);
            else if (Same(key, "SiltWellDeductDepth700")) a.SiltWellDeductDepth700 = Number(value, a.SiltWellDeductDepth700);
            else if (Same(key, "GroundElevation")) a.GroundElevation = Number(value, a.GroundElevation);
            else if (Same(key, "WellDepth")) a.WellDepth = Number(value, a.WellDepth);
            else if (Same(key, "ShaftLength")) a.ShaftLength = Number(value, a.ShaftLength);
            else if (Same(key, "ExcavationLength")) a.ExcavationLength = Number(value, a.ExcavationLength);
            else if (Same(key, "ExcavationWidth")) a.ExcavationWidth = Number(value, a.ExcavationWidth);
            else if (Same(key, "CoverPlate")) a.CoverPlate = value;
            else if (Same(key, "SandCushionThickness")) a.SandCushionThickness = Number(value, a.SandCushionThickness);
            else if (Same(key, "GravelCushionThickness")) a.GravelCushionThickness = Number(value, a.GravelCushionThickness);
            else if (Same(key, "C25RestoreThickness")) a.C25RestoreThickness = Number(value, a.C25RestoreThickness);
            else if (Same(key, "PipeOuterDiameter")) a.PipeOuterDiameter = Number(value, a.PipeOuterDiameter);
            else if (Same(key, "DeductPipeVolume")) a.DeductPipeVolume = Bool(value, a.DeductPipeVolume);
            else if (Same(key, "Remark")) a.Remark = value;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool Bool(string value, bool fallback)
        {
            return QuantityPipeAttributes.ParseBool(value, fallback);
        }

        private static double Number(string value, double fallback)
        {
            return QuantityPipeAttributes.ParseDouble(value, fallback);
        }
    }
}
