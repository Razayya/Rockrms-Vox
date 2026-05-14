using Rock.Plugin;

namespace com.razayya.CustomPersonAttributeSyncEngine.Migrations
{
    [MigrationNumber( 7, "1.15.0" )]
    public class RegisterCategoryAttribute : Migration
    {
        private const string CategoryAttributeGuid = "A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D";
        private const string TreeViewBlockTypeGuid = "B2C3D4E5-F6A7-4B8C-9D0E-1F2A3B4C5D6E";
        private const string TreeViewBlockGuid_GroupPage = "C3D4E5F6-A7B8-4C9D-0E1F-2A3B4C5D6E7F";
        private const string TreeViewBlockGuid_SubGroupPage = "D4E5F6A7-B8C9-4D0E-1F2A-3B4C5D6E7F8A";
        private const string TreeViewBlockGuid_CalcPage = "E5F6A7B8-C9D0-4E1F-2A3B-4C5D6E7F8A9B";

        // Page GUIDs from migration 004/006
        private const string GroupDetailPageGuid = "3B4C5D6E-7F8A-4B9C-0D1E-2F3A4B5C6D7E";
        private const string SubGroupDetailPageGuid = "4C5D6E7F-8A9B-4C0D-1E2F-3A4B5C6D7E8F";
        private const string CalcDetailPageGuid = "5D6E7F8A-9B0C-4D1E-2F3A-4B5C6D7E8F9A";
        private const string LeftSidebarLayoutGuid = "0CB60906-6B74-44FD-AB25-026050EF70EB";

        public override void Up()
        {
            // 1. Register Categories attribute on CalculationGroup
            Sql( $@"
DECLARE @EntityTypeId INT = (SELECT [Id] FROM [EntityType] WHERE [Name] = 'com.razayya.CustomPersonAttributeSyncEngine.Model.CalculationGroup')
DECLARE @FieldTypeId INT = (SELECT [Id] FROM [FieldType] WHERE [Guid] = '775899FB-AC17-4C2C-B809-CF3A1D2AA4E1')

IF NOT EXISTS (SELECT 1 FROM [Attribute] WHERE [Guid] = '{CategoryAttributeGuid}')
BEGIN
    INSERT INTO [Attribute] (
        [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
        [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired],
        [Guid], [AllowSearch], [IsIndexEnabled], [IsAnalytic], [IsAnalyticHistory], [IsActive], [EnableHistory]
    ) VALUES (
        0, @FieldTypeId, @EntityTypeId, '', '',
        'PersonAttributeCategories', 'Person Attribute Categories',
        'Restrict which Person Attribute categories are available as targets for calculations in this group. Leave blank to allow all.',
        0, 0, 0, 0,
        '{CategoryAttributeGuid}', 0, 0, 0, 0, 1, 0
    )

    DECLARE @AttributeId INT = SCOPE_IDENTITY()

    INSERT INTO [AttributeQualifier] ([IsSystem], [AttributeId], [Key], [Value], [Guid])
    VALUES (0, @AttributeId, 'entityTypeName', 'Rock.Model.Attribute', NEWID())

    INSERT INTO [AttributeQualifier] ([IsSystem], [AttributeId], [Key], [Value], [Guid])
    VALUES (0, @AttributeId, 'qualifierColumn', '', NEWID())

    INSERT INTO [AttributeQualifier] ([IsSystem], [AttributeId], [Key], [Value], [Guid])
    VALUES (0, @AttributeId, 'qualifierValue', '', NEWID())
END
" );

            // 2. Register the TreeView block type
            Sql( $@"
IF NOT EXISTS (SELECT 1 FROM [BlockType] WHERE [Guid] = '{TreeViewBlockTypeGuid}')
BEGIN
    INSERT INTO [BlockType] ([IsSystem], [Path], [Name], [Description], [Guid], [IsCommon])
    VALUES (0,
        '~/Plugins/com_razayya/CustomPersonAttributeSyncEngine/CalculationTreeView.ascx',
        'Calculation Tree View',
        'Displays a navigable tree of a Calculation Group hierarchy.',
        '{TreeViewBlockTypeGuid}', 0)
END
" );

            // 3. Switch detail pages to Left Sidebar layout
            Sql( $@"
DECLARE @LayoutId INT = (SELECT [Id] FROM [Layout] WHERE [Guid] = '{LeftSidebarLayoutGuid}')
UPDATE [Page] SET [LayoutId] = @LayoutId WHERE [Guid] IN ('{GroupDetailPageGuid}', '{SubGroupDetailPageGuid}', '{CalcDetailPageGuid}')
" );

            // 4. Add tree view block to Sidebar zone on each detail page
            Sql( $@"
DECLARE @BlockTypeId INT = (SELECT [Id] FROM [BlockType] WHERE [Guid] = '{TreeViewBlockTypeGuid}')

-- Group Detail page
IF NOT EXISTS (SELECT 1 FROM [Block] WHERE [Guid] = '{TreeViewBlockGuid_GroupPage}')
BEGIN
    INSERT INTO [Block] ([IsSystem], [PageId], [BlockTypeId], [Zone], [Order], [Name], [OutputCacheDuration], [Guid])
    SELECT 0, p.[Id], @BlockTypeId, 'Sidebar1', 0, 'Calculation Tree View', 0, '{TreeViewBlockGuid_GroupPage}'
    FROM [Page] p WHERE p.[Guid] = '{GroupDetailPageGuid}'
END

-- Sub Group Detail page
IF NOT EXISTS (SELECT 1 FROM [Block] WHERE [Guid] = '{TreeViewBlockGuid_SubGroupPage}')
BEGIN
    INSERT INTO [Block] ([IsSystem], [PageId], [BlockTypeId], [Zone], [Order], [Name], [OutputCacheDuration], [Guid])
    SELECT 0, p.[Id], @BlockTypeId, 'Sidebar1', 0, 'Calculation Tree View', 0, '{TreeViewBlockGuid_SubGroupPage}'
    FROM [Page] p WHERE p.[Guid] = '{SubGroupDetailPageGuid}'
END

-- Calculation Detail page
IF NOT EXISTS (SELECT 1 FROM [Block] WHERE [Guid] = '{TreeViewBlockGuid_CalcPage}')
BEGIN
    INSERT INTO [Block] ([IsSystem], [PageId], [BlockTypeId], [Zone], [Order], [Name], [OutputCacheDuration], [Guid])
    SELECT 0, p.[Id], @BlockTypeId, 'Sidebar1', 0, 'Calculation Tree View', 0, '{TreeViewBlockGuid_CalcPage}'
    FROM [Page] p WHERE p.[Guid] = '{CalcDetailPageGuid}'
END
" );

            // 5. Set LinkedPage block attributes for each tree view instance
            SetTreeViewLinkedPages( TreeViewBlockGuid_GroupPage );
            SetTreeViewLinkedPages( TreeViewBlockGuid_SubGroupPage );
            SetTreeViewLinkedPages( TreeViewBlockGuid_CalcPage );
        }

        private void SetTreeViewLinkedPages( string blockGuid )
        {
            Sql( $@"
DECLARE @BlockId INT = (SELECT [Id] FROM [Block] WHERE [Guid] = '{blockGuid}')
DECLARE @BlockTypeId INT = (SELECT [Id] FROM [BlockType] WHERE [Guid] = '{TreeViewBlockTypeGuid}')
DECLARE @TextFieldTypeId INT = (SELECT [Id] FROM [FieldType] WHERE [Guid] = '9C204CD0-1233-41C5-818A-C5DA439445AA')

-- Ensure attributes exist
IF NOT EXISTS (SELECT 1 FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'GroupDetailPage')
BEGIN
    INSERT INTO [Attribute] ([IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
        [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid], [AllowSearch], [IsIndexEnabled], [IsAnalytic], [IsAnalyticHistory], [IsActive], [EnableHistory])
    VALUES (0, @TextFieldTypeId,
        (SELECT [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.Block'),
        'BlockTypeId', CAST(@BlockTypeId AS NVARCHAR),
        'GroupDetailPage', 'Group Detail Page', 'Page that shows the Calculation Group detail.',
        0, 0, 0, 1, NEWID(), 0, 0, 0, 0, 1, 0)
END
IF NOT EXISTS (SELECT 1 FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'SubGroupDetailPage')
BEGIN
    INSERT INTO [Attribute] ([IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
        [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid], [AllowSearch], [IsIndexEnabled], [IsAnalytic], [IsAnalyticHistory], [IsActive], [EnableHistory])
    VALUES (0, @TextFieldTypeId,
        (SELECT [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.Block'),
        'BlockTypeId', CAST(@BlockTypeId AS NVARCHAR),
        'SubGroupDetailPage', 'Sub Group Detail Page', 'Page that shows the Calculation Sub Group detail.',
        1, 0, 0, 1, NEWID(), 0, 0, 0, 0, 1, 0)
END
IF NOT EXISTS (SELECT 1 FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'CalculationDetailPage')
BEGIN
    INSERT INTO [Attribute] ([IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue],
        [Key], [Name], [Description], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid], [AllowSearch], [IsIndexEnabled], [IsAnalytic], [IsAnalyticHistory], [IsActive], [EnableHistory])
    VALUES (0, @TextFieldTypeId,
        (SELECT [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.Block'),
        'BlockTypeId', CAST(@BlockTypeId AS NVARCHAR),
        'CalculationDetailPage', 'Calculation Detail Page', 'Page that shows the Calculation detail.',
        2, 0, 0, 1, NEWID(), 0, 0, 0, 0, 1, 0)
END

-- Set attribute values for this block instance
DECLARE @GroupDetailAttrId INT = (SELECT [Id] FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'GroupDetailPage')
DECLARE @SubGroupDetailAttrId INT = (SELECT [Id] FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'SubGroupDetailPage')
DECLARE @CalcDetailAttrId INT = (SELECT [Id] FROM [Attribute] WHERE [EntityTypeQualifierValue] = CAST(@BlockTypeId AS NVARCHAR) AND [Key] = 'CalculationDetailPage')

IF NOT EXISTS (SELECT 1 FROM [AttributeValue] WHERE [AttributeId] = @GroupDetailAttrId AND [EntityId] = @BlockId)
    INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid]) VALUES (0, @GroupDetailAttrId, @BlockId, '{GroupDetailPageGuid}', NEWID())
IF NOT EXISTS (SELECT 1 FROM [AttributeValue] WHERE [AttributeId] = @SubGroupDetailAttrId AND [EntityId] = @BlockId)
    INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid]) VALUES (0, @SubGroupDetailAttrId, @BlockId, '{SubGroupDetailPageGuid}', NEWID())
IF NOT EXISTS (SELECT 1 FROM [AttributeValue] WHERE [AttributeId] = @CalcDetailAttrId AND [EntityId] = @BlockId)
    INSERT INTO [AttributeValue] ([IsSystem], [AttributeId], [EntityId], [Value], [Guid]) VALUES (0, @CalcDetailAttrId, @BlockId, '{CalcDetailPageGuid}', NEWID())
" );
        }

        public override void Down()
        {
            // Remove tree view blocks
            Sql( $@"
DELETE FROM [AttributeValue] WHERE [EntityId] IN (SELECT [Id] FROM [Block] WHERE [Guid] IN ('{TreeViewBlockGuid_GroupPage}', '{TreeViewBlockGuid_SubGroupPage}', '{TreeViewBlockGuid_CalcPage}'))
DELETE FROM [Block] WHERE [Guid] IN ('{TreeViewBlockGuid_GroupPage}', '{TreeViewBlockGuid_SubGroupPage}', '{TreeViewBlockGuid_CalcPage}')
" );

            // Revert pages to Full Width layout
            Sql( $@"
DECLARE @FullWidthLayoutId INT = (SELECT [Id] FROM [Layout] WHERE [Guid] = 'D65F783D-87A9-4CC9-8110-E83466A0EADB')
UPDATE [Page] SET [LayoutId] = @FullWidthLayoutId WHERE [Guid] IN ('{GroupDetailPageGuid}', '{SubGroupDetailPageGuid}', '{CalcDetailPageGuid}')
" );

            // Remove block type
            Sql( $"DELETE FROM [BlockType] WHERE [Guid] = '{TreeViewBlockTypeGuid}'" );

            // Remove category attribute
            Sql( $@"
DELETE FROM [AttributeValue] WHERE [AttributeId] = (SELECT [Id] FROM [Attribute] WHERE [Guid] = '{CategoryAttributeGuid}')
DELETE FROM [AttributeQualifier] WHERE [AttributeId] = (SELECT [Id] FROM [Attribute] WHERE [Guid] = '{CategoryAttributeGuid}')
DELETE FROM [Attribute] WHERE [Guid] = '{CategoryAttributeGuid}'
" );
        }
    }
}
