using Rock.Data;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    public class CalculationRunService : Service<CalculationRun>
    {
        public CalculationRunService( RockContext context ) : base( context ) { }
    }
}
