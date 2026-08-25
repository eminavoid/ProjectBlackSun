using System;
using UnityEngine;

/// <summary>
/// Option module: suma o resta influencia fija en la zona de la seed (o zona seleccionada).
/// </summary>
[Serializable]
public class ChangeZoneInfluence : OptionModule
{
    [SerializeField] private FactionId faction = FactionId.Player;
    [SerializeField] private int amount = 5;
    [SerializeField] private bool useSeedZone = true;

    public override bool CanExecute() => !InfluenceManager.IsNull;

    public override void Execute(Option option, Seed seed)
    {
        DistrictZone zone = null;
        if (useSeedZone && seed != null) zone = seed.CurrentZone;
        if (zone == null) zone = DistrictSelectionController.SelectedZone;
        if (zone == null) return;

        InfluenceManager.Get.ApplyEventInfluence(zone, faction, amount);
    }
}
