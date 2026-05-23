using Rock.Data;

namespace com.razayya.JourneyTrack.Model
{
    public class JourneyProgramService : Service<JourneyProgram>
    {
        public JourneyProgramService( RockContext context ) : base( context ) { }
    }
}
