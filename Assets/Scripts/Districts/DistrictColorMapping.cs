using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Color Mapping", menuName = "Districts/Color Mapping", order = 2)]
public class DistrictColorMapping : ScriptableObject
{
    [Serializable]
    public struct DistrictColorEntry
    {
        public string partName;
        public Districts district;
    }

    [SerializeField] private List<DistrictColorEntry> entries = new List<DistrictColorEntry>
    {
        new DistrictColorEntry { partName = "Red", district = Districts.District1 },
        new DistrictColorEntry { partName = "Blue", district = Districts.District2 },
        new DistrictColorEntry { partName = "Green", district = Districts.District3 },
        new DistrictColorEntry { partName = "Yellow", district = Districts.District4 },
        new DistrictColorEntry { partName = "Purple", district = Districts.District5 },
        new DistrictColorEntry { partName = "White", district = Districts.District6 },
    };

    public bool TryGetDistrictForPart(string partName, out Districts district)
    {
        district = default;
        if (string.IsNullOrWhiteSpace(partName)) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].partName, partName, StringComparison.OrdinalIgnoreCase)) continue;
            district = entries[i].district;
            return true;
        }

        return false;
    }

    public bool TryGetPartNamesForDistrict(Districts district, out List<string> partNames)
    {
        partNames = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].district != district) continue;
            partNames.Add(entries[i].partName);
        }

        return partNames.Count > 0;
    }

    public string FormatDistrictWithColors(Districts district)
    {
        if (!TryGetPartNamesForDistrict(district, out List<string> partNames))
        {
            return district.ToString();
        }

        return $"{district} ({string.Join(", ", partNames)})";
    }

    /// <summary>Nombre de mapa (Red, Blue, …). Usa el mapping del asset, o el default del Vertical Slice.</summary>
    public string GetDisplayName(Districts district)
    {
        if (TryGetPartNamesForDistrict(district, out List<string> partNames) && partNames.Count > 0)
        {
            return partNames.Count == 1 ? partNames[0] : string.Join(", ", partNames);
        }

        return GetDefaultDisplayName(district);
    }

    public static string GetDefaultDisplayName(Districts district)
    {
        switch (district)
        {
            case Districts.District1: return "Red";
            case Districts.District2: return "Blue";
            case Districts.District3: return "Green";
            case Districts.District4: return "Yellow";
            case Districts.District5: return "Purple";
            case Districts.District6: return "White";
            default: return district.ToString();
        }
    }

    public static string GetDisplayName(Districts district, DistrictColorMapping mapping)
    {
        return mapping != null ? mapping.GetDisplayName(district) : GetDefaultDisplayName(district);
    }

    /// <summary>Legacy zone names like Red.001</summary>
    public bool TryGetDistrictFromZoneName(string objectName, out Districts district)
    {
        district = default;
        if (!TryParseColorKeyFromObjectName(objectName, out string colorKey)) return false;
        return TryGetDistrictForPart(colorKey, out district);
    }

    public static bool TryParseColorKeyFromObjectName(string objectName, out string colorKey)
    {
        colorKey = null;
        if (string.IsNullOrWhiteSpace(objectName)) return false;

        int dotIndex = objectName.IndexOf('.');
        colorKey = dotIndex > 0 ? objectName.Substring(0, dotIndex) : objectName;
        return !string.IsNullOrWhiteSpace(colorKey);
    }
}
