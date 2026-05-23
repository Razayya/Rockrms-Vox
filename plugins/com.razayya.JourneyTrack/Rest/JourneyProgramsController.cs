using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Rest.Filters;

namespace com.razayya.JourneyTrack.Rest
{
    /// <summary>
    /// REST endpoints for JourneyTrack: per-person Journey Program sync.
    /// </summary>
    public class JourneyProgramsController : Rock.Rest.ApiController<JourneyProgram>
    {
        public JourneyProgramsController() : base( new JourneyProgramService( new RockContext() ) ) { }

        /// <summary>
        /// Sync a single person against a single Journey Program.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Sync/{programId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF01234567" )]
        public SyncSummary SyncProgramForPerson( int programId, int personId )
        {
            var service = new JourneyTrackService();
            var result = service.ProcessGroupForPerson( programId, personId );

            return new SyncSummary
            {
                PersonId = personId,
                ProgramId = programId,
                Matched = result.MatchedPersonIds?.Count ?? 0,
                Updated = result.Updated,
                Skipped = result.Skipped,
                Errors = result.Errors?.ToList() ?? new List<string>(),
                Log = result.Log?.ToList() ?? new List<string>()
            };
        }

        /// <summary>
        /// Sync a list of people against a single Journey Program.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Sync/{programId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF01234568" )]
        public BatchSyncSummary SyncProgramForPersons( int programId, [FromBody] int[] personIds )
        {
            var batch = new BatchSyncSummary { ProgramId = programId };
            if ( personIds == null || personIds.Length == 0 )
            {
                return batch;
            }

            var service = new JourneyTrackService();
            foreach ( var personId in personIds.Distinct() )
            {
                var r = service.ProcessGroupForPerson( programId, personId );
                batch.Persons.Add( new SyncSummary
                {
                    PersonId = personId,
                    ProgramId = programId,
                    Matched = r.MatchedPersonIds?.Count ?? 0,
                    Updated = r.Updated,
                    Skipped = r.Skipped,
                    Errors = r.Errors?.ToList() ?? new List<string>(),
                    Log = null  // omit per-person log in batch to keep payload small
                } );
                batch.TotalUpdated += r.Updated;
                batch.TotalSkipped += r.Skipped;
                batch.TotalErrors += ( r.Errors?.Count ?? 0 );
            }
            return batch;
        }

        /// <summary>
        /// Read-only progress for a single person across a single Journey Program. No writes.
        /// </summary>
        [Authenticate, Secured]
        [HttpGet]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Progress/{programId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF01234569" )]
        public ProgramProgressResult GetProgress( int programId, int personId )
        {
            return new JourneyTrackService().GetProgramProgressForPerson( programId, personId );
        }
    }

    public class SyncSummary
    {
        public int PersonId { get; set; }
        public int ProgramId { get; set; }
        public int Matched { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Log { get; set; }
    }

    public class BatchSyncSummary
    {
        public int ProgramId { get; set; }
        public int TotalUpdated { get; set; }
        public int TotalSkipped { get; set; }
        public int TotalErrors { get; set; }
        public List<SyncSummary> Persons { get; set; } = new List<SyncSummary>();
    }
}
