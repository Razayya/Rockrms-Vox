using Rock.Data;

namespace com.razayya.JourneyTrack.Model
{
    public class StageService : Service<Stage>
    {
        public StageService( RockContext context ) : base( context ) { }
    }
}
