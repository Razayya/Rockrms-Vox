using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

//using com.bemaservices.BemaPipeline.SystemGuid;
using Rock.Web.Cache;
using Rock.Lava.Blocks;
using System.Security.AccessControl;
using Rock;

namespace com.bemaservices.BemaPipeline.Migrations
{
    [MigrationNumber( 5, "1.12.5" )]
    public class ActionTypeChanges : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            Sql( @"            
                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineActionType] ADD Description [nvarchar](max) NULL;
            " );
            Sql( @"
                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineAction] DROP CONSTRAINT [FK__com_bemaservices_BemaPipeline_BemaPipelineAction_BemaPipelineActionType];
            " );
            Sql( @"            
                ALTER TABLE [_com_bemaservices_BemaPipeline_BemaPipelineAction] ALTER COLUMN [BemaPipelineActionTypeId] [int] NULL;
            " );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
        
        }
    }
}
