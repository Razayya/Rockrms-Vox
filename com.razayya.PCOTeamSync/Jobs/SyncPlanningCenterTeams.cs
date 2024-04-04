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

            var updateCutOff = lastProcessed?.StartDateTime ?? DateTime.MinValue;

            var pcoTeamSync = new PCOTeamSync( applicationId, secret );

            List<string> errors = new List<string>();

            Result = $"Synchronization started.  Syncing data since: {updateCutOff}";

            pcoTeamSync.ImportServiceTypes( out errors );

            if ( errors.Count > 0 )
            {
                Result += "<br/>" + string.Join( "<br/>", errors );
            }

            pcoTeamSync.SyncPeopleData( out errors, updateCutOff );

            if (errors.Count > 0)
            {
                Result += "<br/>" + string.Join( "<br/>", errors );
            }

            pcoTeamSync.SyncTeamsAndPositions(out errors);

            if (errors.Count > 0)
            {
                Result += "<br/>" + string.Join( "<br/>", errors );
            }

            Result += "<br/>Synchronization was completed.";
        }
    }
}
