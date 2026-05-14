namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    /// <summary>
    /// Determines what happens when a person does not match the calculation criteria.
    /// </summary>
    public enum NoMatchBehavior
    {
        /// <summary>
        /// Leave the existing attribute value unchanged.
        /// </summary>
        LeaveUnchanged = 0,

        /// <summary>
        /// Write a specific value using the NoMatchLavaTemplate.
        /// </summary>
        WriteLava = 1
    }

    /// <summary>
    /// Comparison operators used by Person Filter and Completion calculation types.
    /// </summary>
    public enum ComparisonType
    {
        EqualTo = 0,
        NotEqualTo = 1,
        IsNotBlank = 2,
        IsBlank = 3,
        GreaterThan = 4,
        LessThan = 5,
        GreaterThanOrEqualTo = 6,
        LessThanOrEqualTo = 7,
        Contains = 8
    }
}
