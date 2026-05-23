namespace com.razayya.JourneyTrack.Model
{
    /// <summary>
    /// Determines what happens when a person does not match the JourneyCalculation criteria.
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
    /// Comparison operators used by Person Filter and Completion JourneyCalculation types.
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

    /// <summary>
    /// Logic that decides whether a person has completed a JourneyProgram for
    /// the purposes of the program-level rollup attribute.
    /// </summary>
    public enum JourneyProgramCompletionLogic
    {
        /// <summary>
        /// Person must pass every active Stage's Completion calculation.
        /// </summary>
        AllStagesPass = 0
    }

    /// <summary>
    /// Identifies which level of object queued a JourneyCommunicationLog row.
    /// </summary>
    public enum CommunicationContextType
    {
        /// <summary>
        /// Queued by a single JourneyCalculation's OnMatchSystemCommunication.
        /// </summary>
        Calculation = 0,

        /// <summary>
        /// Queued by a Stage's OnCompleteSystemCommunication.
        /// </summary>
        Stage = 1,

        /// <summary>
        /// Queued by a JourneyProgram's OnCompleteSystemCommunication (rollup).
        /// </summary>
        JourneyProgram = 2
    }
}
