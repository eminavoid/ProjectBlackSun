using UnityEngine;

/// <summary>
/// Marks a district container in the map hierarchy (e.g. "Green", "Red").
/// Child mesh zones inherit this district.
/// </summary>
[DisallowMultipleComponent]
public class DistrictPart : MonoBehaviour
{
    [SerializeField] private Districts district;

    public Districts District => district;

    public void SetDistrict(Districts value)
    {
        district = value;
    }

    public void ApplyMapping(DistrictColorMapping mapping)
    {
        if (mapping == null) return;
        if (!mapping.TryGetDistrictForPart(gameObject.name, out Districts mapped)) return;
        district = mapped;
    }
}
