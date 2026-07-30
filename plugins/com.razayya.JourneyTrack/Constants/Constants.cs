namespace com.razayya.JourneyTrack.Constants
{
    public static class AttributeKey
    {
        // Job attributes
        public const string StartTime = "StartTime";
        public const string ShowDebug = "ShowDebug";

        // Attendance JourneyCalculation
        public const string GroupTypes = "GroupTypes";
        public const string MinimumCount = "MinimumCount";
        public const string WithinDays = "WithinDays";
        public const string Schedule = "Schedule";
        public const string Campus = "Campus";

        // Person filter JourneyCalculation
        public const string FilterConditions = "FilterConditions";
        public const string MatchAll = "MatchAll";

        // DataView inclusion JourneyCalculation
        public const string DataView = "DataView";

        // Group type membership JourneyCalculation
        public const string GroupTypes_Membership = "GroupTypes_Membership";
        public const string GroupRole = "GroupRole";
        public const string ActiveMembersOnly = "ActiveMembersOnly";

        // Group attendance JourneyCalculation (attendance against specific groups).
        // MinimumCount / WithinDays are shared keys — they resolve to distinct Attribute
        // rows because each calc type qualifies by its own CalculationTypeEntityTypeId.
        public const string Groups_GroupAttendance = "Groups_GroupAttendance";

        // Group membership JourneyCalculation
        public const string Group = "Group";
        public const string IncludeChildGroups = "IncludeChildGroups";
        public const string GroupRole_GroupMembership = "GroupRole_GroupMembership";
        public const string ActiveMembersOnly_GroupMembership = "ActiveMembersOnly_GroupMembership";

        // Completion JourneyCalculation
        public const string CompletionCriteria = "CompletionCriteria";

        // Step completion JourneyCalculation
        public const string StepProgram = "StepProgram";
        public const string StepType = "StepType";
        public const string RequireCompletedStatus = "RequireCompletedStatus";
    }

    public static class TableName
    {
        public const string JourneyProgram = "_com_razayya_JourneyTrack_JourneyProgram";
        public const string Stage = "_com_razayya_JourneyTrack_Stage";
        public const string JourneyCalculation = "_com_razayya_JourneyTrack_JourneyCalculation";
        public const string JourneyCalculationRun = "_com_razayya_JourneyTrack_JourneyCalculationRun";
        public const string JourneyCommunicationLog = "_com_razayya_JourneyTrack_JourneyCommunicationLog";
        public const string JourneyProgramEnrollment = "_com_razayya_JourneyTrack_JourneyProgramEnrollment";
        public const string JourneyCalculationSkip = "_com_razayya_JourneyTrack_JourneyCalculationSkip";
    }

    public static class CRON
    {
        public const string NightlySync = "0 0 2 1/1 * ? *"; // 2AM Every day
    }
}
