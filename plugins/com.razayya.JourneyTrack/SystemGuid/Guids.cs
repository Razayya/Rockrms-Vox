namespace com.razayya.JourneyTrack.SystemGuid
{
    public static class EntityType
    {
        // Model EntityTypes (preserved Guid values from original plugin so any existing references stay valid)
        public const string CALCULATION_GROUP        = "F7A2B3C4-D5E6-4A7B-8C9D-0E1F2A3B4C5D";  // JourneyProgram
        public const string CALCULATION_SUB_GROUP    = "A1B2C3D4-E5F6-4A7B-8C9D-1E2F3A4B5C6D";  // Stage
        public const string CALCULATION              = "B2C3D4E5-F6A7-4B8C-9D0E-2F3A4B5C6D7E";  // JourneyCalculation
        public const string CALCULATION_RUN          = "B8C9D0E1-F2A3-4B4C-5D6E-8F9A0B1C2D3E";  // JourneyCalculationRun

        // New in JourneyTrack
        public const string JOURNEY_COMMUNICATION_LOG    = "1F0E2D3C-4B5A-6987-8765-4321FEDCBA09";
        public const string JOURNEY_PROGRAM_ENROLLMENT   = "B4C5D6E7-F809-4A1B-9C2D-E3F4A5B6C7D8";
        public const string ENROLL_PERSON_WORKFLOW_ACTION = "C5D6E7F8-091A-4B2C-AD3E-F4A5B6C7D8E9";

        // Calculation Type components
        public const string CALCULATION_TYPE_ATTENDANCE              = "C3D4E5F6-A7B8-4C9D-0E1F-3A4B5C6D7E8F";
        public const string CALCULATION_TYPE_PERSON_FILTER           = "D4E5F6A7-B8C9-4D0E-1F2A-4B5C6D7E8F9A";
        public const string CALCULATION_TYPE_DATAVIEW                = "E5F6A7B8-C9D0-4E1F-2A3B-5C6D7E8F9A0B";
        public const string CALCULATION_TYPE_GROUP_TYPE_MEMBERSHIP   = "F6A7B8C9-D0E1-4F2A-3B4C-6D7E8F9A0B1C";
        public const string CALCULATION_TYPE_GROUP_MEMBERSHIP        = "C9D0E1F2-A3B4-4C5D-6E7F-8A9B0C1D2E3F";
        public const string CALCULATION_TYPE_COMPLETION              = "A7B8C9D0-E1F2-4A3B-4C5D-7E8F9A0B1C2D";
        public const string CALCULATION_TYPE_STEP_COMPLETION         = "2C3D4E5F-6789-4ABC-DEF0-123456789ABC";
        public const string CALCULATION_TYPE_MEDIA_WATCHED            = "3D4E5F60-789A-4BCD-EF01-23456789ABCD";
    }

    public static class ServiceJob
    {
        public const string JOURNEY_TRACK_ENGINE         = "1A2B3C4D-5E6F-4A7B-8C9D-0E1F2A3B4C5D";
        public const string SEND_JOURNEY_COMMUNICATIONS  = "0123ABCD-EF45-6789-ABCD-EF0123456789";
    }
}
