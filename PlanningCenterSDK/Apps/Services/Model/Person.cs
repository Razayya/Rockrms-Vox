using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlanningCenterSDK.Misc;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public class Person : ArchivableModel
    {
        public string PhotoUrl { get; set; }
        public string PhotoThumbnailUrl { get; set; }
        public string PreferredApp { get; set; }
        public bool? AssignedToRehearsalTeam { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NamePrefix { get; set; }
        public string NameSuffix { get; set; }
        public int? LegacyId { get; set; }
        public string FullName { get; set; }
        public string MaxPermissions { get; set; }
        public string Permissions { get; set; }
        public string Status { get; set; }
        public DateTime? Anniversary { get; set; }
        public DateTime? BirthDate { get; set; }
        public string GivenName { get; set; }
        public string MiddleName { get; set; }
        public string NickName { get; set; }
        public bool? AccessMediaAttachments { get; set; }
        public bool? AccessSongAttachments { get; set; }
        public bool? SiteAdministrator { get; set; }
        public DateTime? LoggedInAt { get; set; }
        public string Notes { get; set; }
        public bool? PassedBackgroundCheck { get; set; }
        public string iCalCode { get; set; }
        public int? PreferredMaxPlansPerDay { get; set; }
        public int? PreferredMaxPlansPerMonth { get; set; }
        public bool? PraiseChartsEnabled { get; set; }
        public string MeTab { get; set; }
        public string PlansTab { get; set; }
        public string SongsTab { get; set; }
        public string MediaTab { get; set; }
        public string PeopleTab { get; set; }
        public bool? CanEditAllPeople { get; set; }
        public bool? CanViewAllPeople { get; set; }
        public Person CreatedBy { get; set; }
        public Person UpdatedBy { get; set; }
        public Stream AvatarUploadStream { get; set; }

        public string RelativePath()
        {
            return "people/";
        }
    }
}
