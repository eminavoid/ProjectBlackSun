using System.Collections.Generic;
using UnityEngine;

public class DoctrinesController : MonoBehaviour
{
    public static event System.Action<Doctrine> DoctrineAdded;

    [SerializeField] private PlayerStats stats;

    [Header("Settings")]
    [SerializeField] private int maxDoctrines = 3;
    [SerializeField] private int changeCooldown = 3;

    private readonly List<Doctrine> kept = new List<Doctrine>();
    private readonly List<Charge> charges = new List<Charge>();

    public PlayerStats Stats => stats;
    public int MaxDoctrines => maxDoctrines;
    public int ChangeCooldown => changeCooldown;
    public IReadOnlyList<Doctrine> Kept => kept;

    public int ReadyCharges
    {
        get
        {
            int ready = 0;
            foreach (Charge charge in charges)
            {
                if (charge.Ready) ready++;
            }

            return ready;
        }
    }

    public int TurnsUntilCharge
    {
        get
        {
            int soonest = 0;
            foreach (Charge charge in charges)
            {
                if (charge.Ready) return 0;
                int left = charge.TurnsLeft;
                if (soonest == 0 || left < soonest) soonest = left;
            }

            return soonest;
        }
    }

    private class Charge
    {
        private readonly int turnsRequired;
        private int turns;

        public Charge(int turnsRequired)
        {
            this.turnsRequired = turnsRequired;
            turns = turnsRequired;
        }

        public bool Ready => turns >= turnsRequired;
        public int TurnsLeft => Mathf.Max(0, turnsRequired - turns);

        public void Spend() => turns = 0;

        public void Tick() => turns++;
    }

    private void Awake()
    {
        GameTime.OnTurnEnded += OnTurnEnded;
        GameTime.OnTurnStarted += OnTurnStarted;

        for (int i = 0; i < maxDoctrines; i++) charges.Add(new Charge(changeCooldown));
    }

    private void OnDestroy()
    {
        GameTime.OnTurnEnded -= OnTurnEnded;
        GameTime.OnTurnStarted -= OnTurnStarted;
    }

    public bool IsKept(Doctrine doctrine) => doctrine != null && kept.Contains(doctrine);

    public bool CanAdopt(Doctrine doctrine)
    {
        return doctrine != null && !kept.Contains(doctrine) && kept.Count < maxDoctrines && ReadyCharges > 0;
    }

    public bool Adopt(Doctrine doctrine)
    {
        if (!CanAdopt(doctrine)) return false;

        ApplyStats(doctrine, 1);
        kept.Add(doctrine);
        foreach (Charge charge in charges)
        {
            if (!charge.Ready) continue;
            charge.Spend();
            break;
        }

        DoctrineAdded?.Invoke(doctrine);
        return true;
    }

    public void Renounce(Doctrine doctrine)
    {
        if (!kept.Remove(doctrine)) return;
        ApplyStats(doctrine, -1);
    }

    public bool HasEquippedDoctrine(string doctrineName)
    {
        if (string.IsNullOrWhiteSpace(doctrineName)) return false;

        foreach (Doctrine doctrine in kept)
        {
            if (doctrine == null || string.IsNullOrWhiteSpace(doctrine.Name)) continue;
            if (string.Equals(doctrine.Name, doctrineName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private void ApplyStats(Doctrine doctrine, int sign)
    {
        foreach (Doctrine.StatUpdate update in doctrine.StatUpdates)
        {
            stats.ChangeStat(update.playerStat, update.changeAmount * sign);
        }
    }

    private void OnTurnStarted()
    {
        foreach (Doctrine doctrine in kept) doctrine.OnTickStart();
    }

    private void OnTurnEnded()
    {
        foreach (Charge charge in charges) charge.Tick();
        foreach (Doctrine doctrine in kept) doctrine.OnTickEnd();
    }
}
