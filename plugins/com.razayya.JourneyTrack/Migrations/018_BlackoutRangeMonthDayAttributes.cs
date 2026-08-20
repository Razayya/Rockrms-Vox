using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    /// <summary>
    /// Reshapes Pathway Blackout Ranges from one encoded string per value ("MM-dd|MM-dd")
    /// to a named value with Start Date / End Date attributes (Month Day field type, so
    /// staff get a month/day picker instead of a format to remember). The resolver
    /// (Logic/BlackoutWindow.cs) and its T-SQL mirror both read the attributes from here on.
    /// </summary>
    [MigrationNumber( 18, "1.15.0" )]
    public class BlackoutRangeMonthDayAttributes : Migration
    {
        public override void Up()
        {
            // Same type, same guid - this only refreshes the description for the new shape.
            RockMigrationHelper.AddDefinedType(
                "Global",
                "Pathway Blackout Ranges",
                "Recurring annual date ranges during which group attendance requirements pause. Each value is one named range with a Start Date and an End Date (month and day, both inclusive; a start later than its end wraps across year-end). Days inside any active range do not consume an attendance calculation's Within Days budget - the lookback window stretches so a planned break doesn't fail people, while attendance recorded during the break still counts.",
                SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES );

            // Defined VALUE attributes. Upserts by key, so an environment where these were
            // already created through the UI keeps its rows and just picks up the flags:
            // system (the resolver depends on them), required (a blank endpoint fails the
            // engine visibly, so the editor should refuse it first), no default.
            RockMigrationHelper.AddDefinedTypeAttribute(
                SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES,
                Rock.SystemGuid.FieldType.MONTH_DAY,
                "Start Date",
                "StartDate",
                "First day of the range (month and day), inclusive.",
                0,
                isGridColumn: true,
                defaultValue: "",
                isMultiValue: false,
                isRequired: true,
                guid: SystemGuid.Attribute.BLACKOUT_RANGE_START_DATE );

            RockMigrationHelper.AddDefinedTypeAttribute(
                SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES,
                Rock.SystemGuid.FieldType.MONTH_DAY,
                "End Date",
                "EndDate",
                "Last day of the range (month and day), inclusive. An end earlier than the start wraps across year-end.",
                1,
                isGridColumn: true,
                defaultValue: "",
                isMultiValue: false,
                isRequired: true,
                guid: SystemGuid.Attribute.BLACKOUT_RANGE_END_DATE );

            // Lift any value still in the legacy MM-dd|MM-dd form into the attributes
            // (migration 017's seed on environments that never touched it), then give the
            // seed its label. Values that already carry dates are left alone.
            Sql( $@"
DECLARE @DefinedTypeId int = ( SELECT [Id] FROM [DefinedType] WHERE [Guid] = '{SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES}' );
DECLARE @StartAttributeId int = ( SELECT [Id] FROM [Attribute] WHERE [Guid] = '{SystemGuid.Attribute.BLACKOUT_RANGE_START_DATE}' );
DECLARE @EndAttributeId int = ( SELECT [Id] FROM [Attribute] WHERE [Guid] = '{SystemGuid.Attribute.BLACKOUT_RANGE_END_DATE}' );

IF @DefinedTypeId IS NOT NULL AND @StartAttributeId IS NOT NULL AND @EndAttributeId IS NOT NULL
BEGIN
    DECLARE @Legacy TABLE ( DefinedValueId int NOT NULL, StartText nvarchar(5) NOT NULL, EndText nvarchar(5) NOT NULL );

    INSERT INTO @Legacy ( DefinedValueId, StartText, EndText )
    SELECT v.[Id],
           CAST( CAST( SUBSTRING( v.[Value], 1, 2 ) AS int ) AS nvarchar(2) ) + N'/' + CAST( CAST( SUBSTRING( v.[Value], 4, 2 ) AS int ) AS nvarchar(2) ),
           CAST( CAST( SUBSTRING( v.[Value], 7, 2 ) AS int ) AS nvarchar(2) ) + N'/' + CAST( CAST( SUBSTRING( v.[Value], 10, 2 ) AS int ) AS nvarchar(2) )
    FROM [DefinedValue] v
    WHERE v.[DefinedTypeId] = @DefinedTypeId
      AND LEN( v.[Value] ) = 11
      AND v.[Value] LIKE '[0-1][0-9]-[0-3][0-9]|[0-1][0-9]-[0-3][0-9]';

    INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid] )
    SELECT 0, @StartAttributeId, l.DefinedValueId, l.StartText, NEWID()
    FROM @Legacy l
    WHERE NOT EXISTS ( SELECT 1 FROM [AttributeValue] av WHERE av.[AttributeId] = @StartAttributeId AND av.[EntityId] = l.DefinedValueId );

    INSERT INTO [AttributeValue] ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid] )
    SELECT 0, @EndAttributeId, l.DefinedValueId, l.EndText, NEWID()
    FROM @Legacy l
    WHERE NOT EXISTS ( SELECT 1 FROM [AttributeValue] av WHERE av.[AttributeId] = @EndAttributeId AND av.[EntityId] = l.DefinedValueId );

    UPDATE [DefinedValue]
    SET [Value] = N'Summer Group Sabbatical'
    WHERE [Guid] = '{SystemGuid.DefinedValue.BLACKOUT_SUMMER_SABBATICAL}'
      AND [Id] IN ( SELECT DefinedValueId FROM @Legacy );
END
" );

            Sql( @"
IF OBJECT_ID(N'[dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart]', N'FN') IS NOT NULL
    DROP FUNCTION [dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart];
" );

            Sql( @"
-- Effective attendance-window start for a JourneyCalculation, honoring its Blackout
-- Ranges setting. T-SQL mirror of com.razayya.JourneyTrack.Logic.BlackoutWindow —
-- the two MUST stay in lockstep; change both or neither.
--
-- Semantics (pinned):
--   * Window start = the LATEST date D <= AsOf such that the count of non-blackout
--     days in [D, AsOf) — D included, AsOf excluded — reaches the calc's WithinDays.
--     With no blackout configured this is exactly AsOf - WithinDays.
--   * A blackout day = any date whose month-day falls inside an ACTIVE defined value
--     of the configured type. Each value carries Start Date / End Date attributes
--     (Month Day field type, stored ""M/d""; zero padding tolerated), both inclusive;
--     a start later than its end wraps across year-end. Union semantics across values.
--   * @AsOfDate NULL = Eastern today (this org is single-timezone Eastern; the C# side
--     uses RockDateTime.Today). Never use GETDATE() here — Azure SQL server time is
--     UTC and rolls to tomorrow at 8 PM Eastern.
--   * Returns NULL — deliberately failing VISIBLE rather than silently flat — when the
--     calc id is unknown, WithinDays has no stored value, the configured defined type
--     or its Start/End attributes are missing, or any active value has a blank or
--     malformed endpoint. Callers filtering >= NULL get no rows, which is an obvious
--     symptom instead of a quietly wrong gate.
--   * Pathological configs (blackout covering nearly the whole year) clamp the walk at
--     WithinDays + 400 calendar days, matching the C# bound.
--
-- Call it ONCE per calc (CROSS APPLY or a variable), never inside a per-row predicate.
CREATE FUNCTION [dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart]
(
    @JourneyCalculationId int,
    @AsOfDate date = NULL
)
RETURNS date
AS
BEGIN
    DECLARE @AsOf date = ISNULL( @AsOfDate, CAST( SYSDATETIMEOFFSET() AT TIME ZONE 'Eastern Standard Time' AS date ) );

    DECLARE @CalcEntityTypeId int = ( SELECT TOP 1 [Id] FROM [EntityType] WHERE [Name] = N'com.razayya.JourneyTrack.Model.JourneyCalculation' );

    DECLARE @WithinDays int = (
        SELECT MAX( TRY_CAST( av.[Value] AS int ) )
        FROM [AttributeValue] av
        INNER JOIN [Attribute] a ON a.[Id] = av.[AttributeId]
        WHERE av.[EntityId] = @JourneyCalculationId
          AND a.[EntityTypeId] = @CalcEntityTypeId
          AND a.[Key] = N'WithinDays'
          AND ISNULL( av.[Value], N'' ) <> N'' );

    IF @WithinDays IS NULL
        RETURN NULL;

    DECLARE @BlackoutTypeGuid uniqueidentifier = (
        SELECT MAX( TRY_CAST( av.[Value] AS uniqueidentifier ) )
        FROM [AttributeValue] av
        INNER JOIN [Attribute] a ON a.[Id] = av.[AttributeId]
        WHERE av.[EntityId] = @JourneyCalculationId
          AND a.[EntityTypeId] = @CalcEntityTypeId
          AND a.[Key] = N'BlackoutRanges'
          AND ISNULL( av.[Value], N'' ) <> N'' );

    IF @BlackoutTypeGuid IS NULL
        RETURN DATEADD( day, -@WithinDays, @AsOf );

    DECLARE @BlackoutTypeId int = ( SELECT [Id] FROM [DefinedType] WHERE [Guid] = @BlackoutTypeGuid );

    IF @BlackoutTypeId IS NULL
        RETURN NULL;

    DECLARE @DefinedValueEntityTypeId int = ( SELECT TOP 1 [Id] FROM [EntityType] WHERE [Name] = N'Rock.Model.DefinedValue' );

    DECLARE @StartAttributeId int = (
        SELECT TOP 1 [Id] FROM [Attribute]
        WHERE [EntityTypeId] = @DefinedValueEntityTypeId
          AND [EntityTypeQualifierColumn] = N'DefinedTypeId'
          AND [EntityTypeQualifierValue] = CAST( @BlackoutTypeId AS varchar(20) )
          AND [Key] = N'StartDate' );

    DECLARE @EndAttributeId int = (
        SELECT TOP 1 [Id] FROM [Attribute]
        WHERE [EntityTypeId] = @DefinedValueEntityTypeId
          AND [EntityTypeQualifierColumn] = N'DefinedTypeId'
          AND [EntityTypeQualifierValue] = CAST( @BlackoutTypeId AS varchar(20) )
          AND [Key] = N'EndDate' );

    IF @StartAttributeId IS NULL OR @EndAttributeId IS NULL
        RETURN NULL;

    -- One row per active value: raw endpoint text, then month*100+day once parsed.
    DECLARE @Ranges TABLE ( StartText nvarchar(50) NULL, EndText nvarchar(50) NULL, StartMD int NULL, EndMD int NULL );

    INSERT INTO @Ranges ( StartText, EndText )
    SELECT LTRIM( RTRIM( sav.[Value] ) ), LTRIM( RTRIM( eav.[Value] ) )
    FROM [DefinedValue] v
    LEFT JOIN [AttributeValue] sav ON sav.[EntityId] = v.[Id] AND sav.[AttributeId] = @StartAttributeId
    LEFT JOIN [AttributeValue] eav ON eav.[EntityId] = v.[Id] AND eav.[AttributeId] = @EndAttributeId
    WHERE v.[DefinedTypeId] = @BlackoutTypeId
      AND v.[IsActive] = 1;

    -- ""M/d"": exactly one slash, an integer on each side. Anything else stays NULL.
    UPDATE @Ranges SET
        StartMD = CASE WHEN StartText LIKE N'%/%' AND StartText NOT LIKE N'%/%/%'
                       THEN TRY_CAST( LEFT( StartText, CHARINDEX( N'/', StartText ) - 1 ) AS int ) * 100
                          + TRY_CAST( SUBSTRING( StartText, CHARINDEX( N'/', StartText ) + 1, 50 ) AS int )
                  END,
        EndMD   = CASE WHEN EndText LIKE N'%/%' AND EndText NOT LIKE N'%/%/%'
                       THEN TRY_CAST( LEFT( EndText, CHARINDEX( N'/', EndText ) - 1 ) AS int ) * 100
                          + TRY_CAST( SUBSTRING( EndText, CHARINDEX( N'/', EndText ) + 1, 50 ) AS int )
                  END;

    -- Any active value with a blank, malformed, or out-of-range endpoint is a config
    -- error: fail visible.
    IF EXISTS ( SELECT 1 FROM @Ranges
                WHERE StartMD IS NULL OR EndMD IS NULL
                   OR StartMD / 100 NOT BETWEEN 1 AND 12 OR StartMD % 100 NOT BETWEEN 1 AND 31
                   OR EndMD   / 100 NOT BETWEEN 1 AND 12 OR EndMD   % 100 NOT BETWEEN 1 AND 31 )
        RETURN NULL;

    IF NOT EXISTS ( SELECT 1 FROM @Ranges )
        RETURN DATEADD( day, -@WithinDays, @AsOf );

    IF @WithinDays <= 0
        RETURN @AsOf;

    DECLARE @D date = @AsOf;
    DECLARE @Counted int = 0;
    DECLARE @Walked int = 0;
    DECLARE @MaxWalk int = @WithinDays + 400;
    DECLARE @MD int;

    WHILE @Counted < @WithinDays AND @Walked < @MaxWalk
    BEGIN
        SET @D = DATEADD( day, -1, @D );
        SET @Walked += 1;
        SET @MD = MONTH( @D ) * 100 + DAY( @D );

        IF NOT EXISTS ( SELECT 1 FROM @Ranges
                        WHERE ( StartMD <= EndMD AND @MD BETWEEN StartMD AND EndMD )
                           OR ( StartMD >  EndMD AND ( @MD >= StartMD OR @MD <= EndMD ) ) )
            SET @Counted += 1;
    END

    RETURN @D;
END
" );
        }

        public override void Down()
        {
            // Rock never runs plugin Down() on its own; this is here for completeness.
            // Dropping the function leaves the mirror surfaces without a window until
            // migration 017's version is reinstated by hand.
            Sql( @"
IF OBJECT_ID(N'[dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart]', N'FN') IS NOT NULL
    DROP FUNCTION [dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart];
" );

            RockMigrationHelper.DeleteAttribute( SystemGuid.Attribute.BLACKOUT_RANGE_START_DATE );
            RockMigrationHelper.DeleteAttribute( SystemGuid.Attribute.BLACKOUT_RANGE_END_DATE );
        }
    }
}
