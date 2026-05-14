using Rock.Data;

namespace com.razayya.CustomPersonAttributeSyncEngine.Model
{
    public class CalculationSubGroupService : Service<CalculationSubGroup>
    {
        public CalculationSubGroupService( RockContext context ) : base( context ) { }
    }
}
