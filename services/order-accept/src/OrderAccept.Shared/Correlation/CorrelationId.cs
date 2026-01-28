namespace OrderAccept.Shared.Correlation
{
    /// <summary>
    /// Represents a unique identifier used to correlate related operations or requests across system boundaries.
    /// </summary>
    /// <remarks>Use <see cref="CorrelationId"/> to track and associate related activities, such as requests
    /// or messages, within distributed systems or logging scenarios. This type is immutable and can be created from an
    /// existing <see cref="System.Guid"/> or generated using <see cref="New"/>.</remarks>
    /// <param name="Value">The underlying <see cref="System.Guid"/> value that uniquely identifies the correlation context.</param>
    public readonly record struct CorrelationId(Guid Value)
    {
        /// <summary>
        /// Creates a new instance of the CorrelationId structure with a unique value.
        /// </summary>
        /// <returns>A CorrelationId initialized with a newly generated unique identifier.</returns>
        public static CorrelationId New() => new(Guid.NewGuid());

        /// <summary>
        /// Returns a string that represents the current value of the object.
        /// </summary>
        /// <returns>A string representation of the value encapsulated by this instance.</returns>
        public override string ToString() => Value.ToString();
    }
}
