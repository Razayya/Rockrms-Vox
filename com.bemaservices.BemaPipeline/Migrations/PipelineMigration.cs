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
    public abstract class PipelineMigration : Migration
    {
        public PipelineMigrationHelper PipelineMigrationHelper
        {
            get
            {
                if ( _pipelineMigrationHelper == null )
                {
                    _pipelineMigrationHelper = new PipelineMigrationHelper( this );
                }
                return _pipelineMigrationHelper;
            }
        }
        private PipelineMigrationHelper _pipelineMigrationHelper = null;
    }
}
