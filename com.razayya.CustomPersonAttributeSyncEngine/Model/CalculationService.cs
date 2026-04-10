using Rock.Data;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    public class CalculationService : Service<Calculation>
    {
        public CalculationService( RockContext context ) : base( context ) { }
    }
}
