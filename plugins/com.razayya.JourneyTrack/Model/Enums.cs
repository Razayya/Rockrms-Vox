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
        WriteLava = 1,

        /// <summary>
        /// Blank the target attribute when the person doesn't match — the step "un-completes"
        /// for anyone who lapses out of it.
        ///
        /// Use on steps whose condition can genuinely lapse (group membership, serving,
        /// attendance windows); leave <see cref="LeaveUnchanged"/> on steps that record
        /// something permanent, where the date is a historical fact rather than a live
        /// state (baptism, a class attended).
        ///
        /// People who already have no value are left alone — the existing diff means no
        /// empty rows get inserted for the whole population, only real values get cleared.
        /// Clearing writes an empty string rather than deleting the row, matching how the
        /// rest of the engine and the UI test for presence (IsNullOrWhiteSpace).
        ///
        /// Note this is deliberately independent of SkipIfTargetHasValue: that flag holds
        /// people back from evaluation precisely because they already have a value, so a
        /// calc with both set will never reach the no-match branch. The two express
        /// opposite intents; don't combine them.
        /// </summary>
        ClearValue = 2
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
    public enum CommunicationContextType : byte
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
