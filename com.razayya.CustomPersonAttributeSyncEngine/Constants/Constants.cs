namespace com.razayya.CustomPersonAttributeSyncEngine.Constants
{
    public static class AttributeKey
    {
        // Job attributes
        public const string StartTime = "StartTime";
        public const string ShowDebug = "ShowDebug";

        // Attendance calculation
        public const string GroupType = "GroupType";
        public const string MinimumCount = "MinimumCount";
        public const string WithinDays = "WithinDays";
        public const string Schedule = "Schedule";
        public const string Campus = "Campus";

        // Person filter calculation
        public const string FilterConditions = "FilterConditions";
        public const string MatchAll = "MatchAll";

        // DataView inclusion calculation
        public const string DataView = "DataView";

        // Group membership calculation
        public const string GroupTypeOrGroup = "GroupTypeOrGroup";
        public const string GroupRole = "GroupRole";
        public const string ActiveMembersOnly = "ActiveMembersOnly";

        // Completion calculation
        public const string CompletionCriteria = "CompletionCriteria";
    }

    public static class TableName
    {
        public const string CalculationGroup = "_com_razayya_CustomPersonAttributeSyncEngine_CalculationGroup";
        public const string CalculationSubGroup = "_com_razayya_CustomPersonAttributeSyncEngine_CalculationSubGroup";
        public const string Calculation = "_com_razayya_CustomPersonAttributeSyncEngine_Calculation";
    }

    public static class CRON
    {
        public const string NightlySync = "0 0 2 1/1 * ? *"; // 2AM Every day
    }
}
