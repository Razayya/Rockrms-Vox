using Rock.Data;

namespace com.razayya.JourneyTrack.Model
{
    public class JourneyCommunicationLogService : Service<JourneyCommunicationLog>
    {
        public JourneyCommunicationLogService( RockContext context ) : base( context ) { }
    }
}
