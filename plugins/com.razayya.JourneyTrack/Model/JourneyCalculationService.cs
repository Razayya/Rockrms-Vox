using Rock.Data;

namespace com.razayya.JourneyTrack.Model
{
    public class JourneyCalculationService : Service<JourneyCalculation>
    {
        public JourneyCalculationService( RockContext context ) : base( context ) { }
    }
}
