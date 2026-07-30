using Rock.Plugin;

namespace com.razayya.JourneyTrack.Migrations
{
    [MigrationNumber( 7, "1.15.0" )]
    public class RegisterMediaWatchedCalc : Migration
    {
        private const string AssemblyName = "com.razayya.JourneyTrack";
        private const string CalcTypeNs   = "com.razayya.JourneyTrack.CalculationTypes";

        // Rock built-in FieldType Guids
        private const string FT_MEDIA_ELEMENT = "A17D5AAC-B7AE-4587-B703-A0FC3625F0F8";  // Rock.Field.Types.MediaElementFieldType
        private const string FT_INTEGER       = "A75DFC58-7A1B-4799-BF31-451B2BBE38FF";  // Rock.Field.Types.IntegerFieldType

        // Config attr Guids (stable across deploys)
        private const string ATTR_MEDIA_ELEMENT = "6a651eb7-b10e-4972-ba25-e39c98d5cc7f";
        private const string ATTR_MIN_WATCHED   = "c72bf71b-3894-4f04-bd65-72d138dc44ac";

        public override void Up()
        {
            // 1. Register the new EntityType (component class)
            RockMigrationHelper.UpdateEntityType(
                $"{CalcTypeNs}.MediaWatchedCalculation",
                "Media Watched Calculation",
                $"{CalcTypeNs}.MediaWatchedCalculation, {AssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                false, true,
                SystemGuid.EntityType.CALCULATION_TYPE_MEDIA_WATCHED );

            // 2. Resolve the new EntityTypeId so we can use it as the qualifier value
            //    for the JourneyCalculation Model attribute qualifier
            //    (CalculationTypeEntityTypeId = <newly-created EntityType Id>).
            //    AddOrUpdateEntityAttribute takes the qualifier value as a string;
            //    we look it up via SQL after the EntityType insert.
            Sql( $@"
                DECLARE @CalcEntityTypeId INT = (
                    SELECT TOP 1 Id FROM EntityType
                    WHERE [Guid] = '{SystemGuid.EntityType.CALCULATION_TYPE_MEDIA_WATCHED}'
                );
                DECLARE @JourneyCalcEntityTypeId INT = (
                    SELECT TOP 1 Id FROM EntityType
                    WHERE Name = 'com.razayya.JourneyTrack.Model.JourneyCalculation'
                );
                DECLARE @MediaFieldTypeId INT = (
                    SELECT TOP 1 Id FROM FieldType WHERE [Guid] = '{FT_MEDIA_ELEMENT}'
                );
                DECLARE @IntegerFieldTypeId INT = (
                    SELECT TOP 1 Id FROM FieldType WHERE [Guid] = '{FT_INTEGER}'
                );

                -- MediaElement config attr
                IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [Guid] = '{ATTR_MEDIA_ELEMENT}')
                BEGIN
                    INSERT INTO Attribute
                        (IsSystem, FieldTypeId, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue,
                         [Key], Name, Description, IsGridColumn, IsMultiValue, AllowSearch, [Order], [IsActive],
                         IsRequired, IsAnalytic, IsAnalyticHistory, EnableHistory, ShowOnBulk, IsPublic,
                         IsIndexEnabled, IsDefaultPersistedValueDirty, IsSuppressHistoryLogging,
                         [Guid], CreatedDateTime, ModifiedDateTime)
                    VALUES
                        (0, @MediaFieldTypeId, @JourneyCalcEntityTypeId,
                         'CalculationTypeEntityTypeId', CAST(@CalcEntityTypeId AS NVARCHAR(10)),
                         'MediaElement', 'Media Element',
                         'The Rock Media Element whose Interaction rows are evaluated for watch coverage.',
                         0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                         '{ATTR_MEDIA_ELEMENT}', GETDATE(), GETDATE());
                END
                ELSE
                BEGIN
                    UPDATE Attribute
                       SET FieldTypeId = @MediaFieldTypeId,
                           EntityTypeId = @JourneyCalcEntityTypeId,
                           EntityTypeQualifierColumn = 'CalculationTypeEntityTypeId',
                           EntityTypeQualifierValue = CAST(@CalcEntityTypeId AS NVARCHAR(10)),
                           IsRequired = 1,
                           [Order] = 0,
                           ModifiedDateTime = GETDATE()
                     WHERE [Guid] = '{ATTR_MEDIA_ELEMENT}';
                END

                -- MinWatchedPercent config attr
                IF NOT EXISTS (SELECT 1 FROM Attribute WHERE [Guid] = '{ATTR_MIN_WATCHED}')
                BEGIN
                    INSERT INTO Attribute
                        (IsSystem, FieldTypeId, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue,
                         [Key], Name, Description, IsGridColumn, IsMultiValue, AllowSearch, [Order], [IsActive],
                         IsRequired, IsAnalytic, IsAnalyticHistory, EnableHistory, ShowOnBulk, IsPublic,
                         IsIndexEnabled, IsDefaultPersistedValueDirty, IsSuppressHistoryLogging,
                         [Guid], CreatedDateTime, ModifiedDateTime, DefaultValue)
                    VALUES
                        (0, @IntegerFieldTypeId, @JourneyCalcEntityTypeId,
                         'CalculationTypeEntityTypeId', CAST(@CalcEntityTypeId AS NVARCHAR(10)),
                         'MinWatchedPercent', 'Min Watched %',
                         'The threshold (0-100) cumulative watched coverage must meet for the person to match. Default 95.',
                         0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                         '{ATTR_MIN_WATCHED}', GETDATE(), GETDATE(), '95');
                END
                ELSE
                BEGIN
                    UPDATE Attribute
                       SET FieldTypeId = @IntegerFieldTypeId,
                           EntityTypeId = @JourneyCalcEntityTypeId,
                           EntityTypeQualifierColumn = 'CalculationTypeEntityTypeId',
                           EntityTypeQualifierValue = CAST(@CalcEntityTypeId AS NVARCHAR(10)),
                           IsRequired = 1,
                           [Order] = 1,
                           DefaultValue = '95',
                           ModifiedDateTime = GETDATE()
                     WHERE [Guid] = '{ATTR_MIN_WATCHED}';
                END
            " );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( ATTR_MIN_WATCHED );
            RockMigrationHelper.DeleteAttribute( ATTR_MEDIA_ELEMENT );
            RockMigrationHelper.DeleteEntityType( SystemGuid.EntityType.CALCULATION_TYPE_MEDIA_WATCHED );
        }
    }
}
