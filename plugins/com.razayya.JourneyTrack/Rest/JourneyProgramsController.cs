using System;
using System.Collections.Generic;
using System.Data.Entity;
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
        /// Sync a single person against a single Stage. Runs the prerequisite cascade
        /// through the target Stage (inclusive) and skips later Stages — cheaper than
        /// the program-scoped sync when mobile only needs the open Stage refreshed.
        /// Does not write the program rollup; a full program sync owns that write.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Sync/Stage/{stageId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF0123456E" )]
        public SyncSummary SyncStageForPerson( int stageId, int personId )
        {
            var service = new JourneyTrackService();
            var result = service.ProcessStageForPerson( stageId, personId );

            return new SyncSummary
            {
                PersonId = personId,
                StageId = stageId,
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

        /// <summary>
        /// Enroll a person in a Program. Idempotent — re-activates a prior inactive row
        /// if present; no-op if already actively enrolled.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Enroll/{programId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF0123456A" )]
        public EnrollmentSummary EnrollPerson( int programId, int personId, string source = "REST" )
        {
            using ( var rockContext = new RockContext() )
            {
                var personAliasId = new PersonAliasService( rockContext ).Queryable()
                    .Where( pa => pa.PersonId == personId && pa.AliasPersonId == personId )
                    .Select( pa => ( int? ) pa.Id )
                    .FirstOrDefault();
                if ( !personAliasId.HasValue )
                {
                    return new EnrollmentSummary { PersonId = personId, ProgramId = programId,
                        Status = "PersonAlias not found" };
                }

                var enrollmentService = new JourneyProgramEnrollmentService( rockContext );
                var existing = enrollmentService.Queryable()
                    .Where( e => e.JourneyProgramId == programId && e.PersonAlias.PersonId == personId )
                    .OrderByDescending( e => e.Id )
                    .FirstOrDefault();

                var nowUtc = RockDateTime.Now;
                var summary = new EnrollmentSummary { PersonId = personId, ProgramId = programId };
                if ( existing == null )
                {
                    var row = new JourneyProgramEnrollment
                    {
                        JourneyProgramId = programId,
                        PersonAliasId = personAliasId.Value,
                        EnrolledDateTime = nowUtc,
                        IsActive = true,
                        Source = string.IsNullOrWhiteSpace( source ) ? "REST" : source
                    };
                    enrollmentService.Add( row );
                    rockContext.SaveChanges();
                    summary.EnrollmentId = row.Id;
                    summary.Status = "Created";
                }
                else if ( !existing.IsActive )
                {
                    existing.IsActive = true;
                    existing.UnenrolledDateTime = null;
                    existing.ModifiedDateTime = nowUtc;
                    rockContext.SaveChanges();
                    summary.EnrollmentId = existing.Id;
                    summary.Status = "Reactivated";
                }
                else
                {
                    summary.EnrollmentId = existing.Id;
                    summary.Status = "AlreadyEnrolled";
                }
                return summary;
            }
        }

        /// <summary>
        /// Soft-unenroll a person from a Program. Keeps the row for audit;
        /// sets IsActive=false + UnenrolledDateTime. No-op if not actively enrolled.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Unenroll/{programId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF0123456B" )]
        public EnrollmentSummary UnenrollPerson( int programId, int personId )
        {
            using ( var rockContext = new RockContext() )
            {
                var enrollmentService = new JourneyProgramEnrollmentService( rockContext );
                var row = enrollmentService.Queryable()
                    .Where( e => e.JourneyProgramId == programId && e.PersonAlias.PersonId == personId && e.IsActive )
                    .OrderByDescending( e => e.Id )
                    .FirstOrDefault();

                var summary = new EnrollmentSummary { PersonId = personId, ProgramId = programId };
                if ( row == null )
                {
                    summary.Status = "NotEnrolled";
                    return summary;
                }
                row.IsActive = false;
                row.UnenrolledDateTime = RockDateTime.Now;
                row.ModifiedDateTime = row.UnenrolledDateTime;
                rockContext.SaveChanges();
                summary.EnrollmentId = row.Id;
                summary.Status = "Unenrolled";
                return summary;
            }
        }

        /// <summary>
        /// Reports the active enrollment status for a (program, person), if any.
        /// </summary>
        [Authenticate, Secured]
        [HttpGet]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/Enrollment/{programId}/{personId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF0123456C" )]
        public EnrollmentSummary GetEnrollment( int programId, int personId )
        {
            using ( var rockContext = new RockContext() )
            {
                var row = new JourneyProgramEnrollmentService( rockContext ).Queryable().AsNoTracking()
                    .Where( e => e.JourneyProgramId == programId && e.PersonAlias.PersonId == personId )
                    .OrderByDescending( e => e.Id )
                    .Select( e => new EnrollmentSummary
                    {
                        EnrollmentId = e.Id,
                        ProgramId = programId,
                        PersonId = personId,
                        IsActive = e.IsActive,
                        EnrolledDateTime = e.EnrolledDateTime,
                        UnenrolledDateTime = e.UnenrolledDateTime,
                        Source = e.Source,
                        Status = e.IsActive ? "Enrolled" : "Unenrolled"
                    } )
                    .FirstOrDefault();
                return row ?? new EnrollmentSummary { ProgramId = programId, PersonId = personId, Status = "NotEnrolled" };
            }
        }

        /// <summary>
        /// Trigger an enrollment reconciliation pass against the Program's population spec.
        /// </summary>
        [Authenticate, Secured]
        [HttpPost]
        [System.Web.Http.Route( "api/com_razayya_JourneyTrack/JourneyPrograms/ReconcileEnrollments/{programId}" )]
        [Rock.SystemGuid.RestActionGuid( "B0E1A2C3-D4E5-4F67-89AB-CDEF0123456D" )]
        public ReconcileResult ReconcileEnrollments( int programId )
        {
            return new JourneyTrackService().ReconcileEnrollments( programId );
        }
    }

    public class EnrollmentSummary
    {
        public int ProgramId { get; set; }
        public int PersonId { get; set; }
        public int EnrollmentId { get; set; }
        public bool IsActive { get; set; }
        public System.DateTime? EnrolledDateTime { get; set; }
        public System.DateTime? UnenrolledDateTime { get; set; }
        public string Source { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// Returned by both program-scoped (<see cref="JourneyProgramsController.SyncProgramForPerson"/>)
    /// and stage-scoped (<see cref="JourneyProgramsController.SyncStageForPerson"/>) endpoints.
    /// Exactly one of <see cref="ProgramId"/> / <see cref="StageId"/> is populated per call
    /// (the other defaults to 0 — the caller already knows the scope from the route they hit).
    /// </summary>
    public class SyncSummary
    {
        public int PersonId { get; set; }
        public int ProgramId { get; set; }
        /// <summary>Set on stage-scoped sync responses; 0 on program-scoped responses.</summary>
        public int StageId { get; set; }
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
