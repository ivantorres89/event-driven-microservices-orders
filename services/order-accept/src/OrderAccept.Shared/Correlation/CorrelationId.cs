namespace OrderAccept.Shared.Correlation
{
    public readonly record struct CorrelationId(Guid Value)
    {
        public static CorrelationId New() => new(Guid.NewGuid());
        public override string ToString() => Value.ToString();
    }
}
