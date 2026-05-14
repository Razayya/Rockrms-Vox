using Rock.Data;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    public class CalculationGroupService : Service<CalculationGroup>
    {
        public CalculationGroupService( RockContext context ) : base( context ) { }
    }
}
