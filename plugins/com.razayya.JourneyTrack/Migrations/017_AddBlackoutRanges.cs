using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 17, "1.15.0" )]
    public class AddBlackoutRanges : Migration
    {
        public override void Up()
        {
            // The defined type is system (can't be deleted out from under the calcs);
            // its values are not (staff maintain the ranges through the UI).
            RockMigrationHelper.AddDefinedType(
                "Global",
                "Pathway Blackout Ranges",
                "Recurring annual date ranges during which group attendance requirements pause. Each value is one range in the form MM-dd|MM-dd (both ends inclusive; a start later than its end wraps across year-end). Days inside any active range do not consume an attendance calculation's Within Days budget — the lookback window stretches so a planned break doesn't fail people, while attendance recorded during the break still counts.",
                SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES );

            RockMigrationHelper.AddDefinedValue(
                SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES,
                "07-05|09-10",
                "Summer group sabbatical",
                SystemGuid.DefinedValue.BLACKOUT_SUMMER_SABBATICAL,
                isSystem: false );

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
--     of the configured type. Value format: MM-dd|MM-dd, both ends inclusive; a start
--     later than its end wraps across year-end. Union semantics across values.
--   * @AsOfDate NULL = Eastern today (this org is single-timezone Eastern; the C# side
--     uses RockDateTime.Today). Never use GETDATE() here — Azure SQL server time is
--     UTC and rolls to tomorrow at 8 PM Eastern.
--   * Returns NULL — deliberately failing VISIBLE rather than silently flat — when the
--     calc id is unknown, WithinDays has no stored value, the configured defined type
--     is missing, or any active value is malformed. Callers filtering >= NULL get no
--     rows, which is an obvious symptom instead of a quietly wrong gate.
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

    IF NOT EXISTS ( SELECT 1 FROM [DefinedType] WHERE [Guid] = @BlackoutTypeGuid )
        RETURN NULL;

    DECLARE @Ranges TABLE ( StartMD int NOT NULL, EndMD int NOT NULL );

    INSERT INTO @Ranges ( StartMD, EndMD )
    SELECT
        TRY_CAST( SUBSTRING( v.[Value], 1, 2 ) AS int ) * 100 + TRY_CAST( SUBSTRING( v.[Value], 4, 2 ) AS int ),
        TRY_CAST( SUBSTRING( v.[Value], 7, 2 ) AS int ) * 100 + TRY_CAST( SUBSTRING( v.[Value], 10, 2 ) AS int )
    FROM [DefinedValue] v
    INNER JOIN [DefinedType] t ON t.[Id] = v.[DefinedTypeId]
    WHERE t.[Guid] = @BlackoutTypeGuid
      AND v.[IsActive] = 1
      AND LEN( v.[Value] ) = 11
      AND v.[Value] LIKE '[0-1][0-9]-[0-3][0-9]|[0-1][0-9]-[0-3][0-9]';

    -- Any active value the format filter rejected, or with an out-of-range month/day,
    -- is a config error: fail visible.
    IF ( SELECT COUNT(*) FROM [DefinedValue] v
         INNER JOIN [DefinedType] t ON t.[Id] = v.[DefinedTypeId]
         WHERE t.[Guid] = @BlackoutTypeGuid AND v.[IsActive] = 1 ) <> ( SELECT COUNT(*) FROM @Ranges )
        RETURN NULL;

    IF EXISTS ( SELECT 1 FROM @Ranges
                WHERE StartMD / 100 NOT BETWEEN 1 AND 12 OR StartMD % 100 NOT BETWEEN 1 AND 31
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
            Sql( @"
IF OBJECT_ID(N'[dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart]', N'FN') IS NOT NULL
    DROP FUNCTION [dbo].[_com_razayya_JourneyTrack_ufnAttendanceWindowStart];
" );

            RockMigrationHelper.DeleteDefinedValue( SystemGuid.DefinedValue.BLACKOUT_SUMMER_SABBATICAL );
            RockMigrationHelper.DeleteDefinedType( SystemGuid.DefinedType.PATHWAY_BLACKOUT_RANGES );
        }
    }
}
