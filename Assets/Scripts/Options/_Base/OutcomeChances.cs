public readonly struct OutcomeChances
{
    public readonly float GoodPercentage => good * 100f;
    public readonly float BadPercentage => bad * 100f;

    public readonly float good;
    public readonly float bad;

    public static OutcomeChances GuarantedPositive => new OutcomeChances(1f, 0f);
    public static OutcomeChances GuarantedNegative => new OutcomeChances(0f, 1f);

    public bool IsUncertain => good > 0.001f && good < 0.999f;

    public OutcomeChances(float good, float bad)
    {
        this.good = good;
        this.bad = bad;
    }
}