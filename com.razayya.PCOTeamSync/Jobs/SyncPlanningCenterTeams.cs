using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quartz;
using Rock.Attribute;
using Rock.Jobs;
using Rock.Web.Cache;

namespace com.razayya.PCOTeamSync.Jobs
{
    [DisplayName("Sync Planning Center Online Teams")]
    [Description("Syncs all People Services, Teams, Positions and Position Assignements between Rock and Planning Center.  Services, Teams, and Positions will only sync from PCO to Rock.  Assignements and Person data will sync both ways, taking which ever record has been modified more recently.")]

    [TextField("Application Id", "The Application Id for your PCO Personal Access Token.", true, isPassword: true, key: AttributeKey.Application_Id )]
    [TextField( "Secret", "The Secret for your PCO Personal Access Token.", true, isPassword: true, key: AttributeKey.Secret )]

    [DisallowConcurrentExecution]
    public class SyncPlanningCenterTeams : RockJob
    {
        private static class AttributeKey
        {
            public const string Application_Id = "ApplicationId";
            public const string Secret = "Secret";
        }

        public override void Execute()
        {

            var lastProcessed = ServiceJob.ServiceJobHistory.Where( j => j.Status == "Success" ).OrderByDescending( j => j.StopDateTime ).FirstOrDefault();

            var applicationId = GetAttributeValue(AttributeKey.Application_Id);
            var secret = GetAttributeValue(AttributeKey.Secret);

            

            var pcoTeamSync = new PCOTeamSync( applicationId, secret );

            pcoTeamSync.ImportServiceTypes();
            pcoTeamSync.SyncPeopleData();
            pcoTeamSync.SyncTeamsAndPositions();

            Result = "Synchronization was completed successfully.";
        }
    }
}
