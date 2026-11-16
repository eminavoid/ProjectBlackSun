public readonly struct OutcomeChances
{
    public readonly float GoodPercentage => good * 100f;
    public readonly float BadPercentage => bad * 100f;

    public readonly float good;
    public readonly float bad;

    public static OutcomeChances GuarantedPositive => new OutcomeChances(100f, 0f);
    public static OutcomeChances GuarantedNegative => new OutcomeChances(0f, 100f);

    public OutcomeChances(float good, float bad)
    {
        this.good = good;
        this.bad = bad;
    }
}