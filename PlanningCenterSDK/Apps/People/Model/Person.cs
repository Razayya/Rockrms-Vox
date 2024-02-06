using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace PlanningCenterSDK.Apps.People.Model
{
    public class Person
    {
        [JsonProperty( propertyName: "id" )]
        public int Id { get; set; }

        [JsonProperty( propertyName: "given_name" )]
        public string GivenName { get; set; }

        [JsonProperty( propertyName: "first_name" )]
        public string FirstName { get; set; }

        [JsonProperty( propertyName: "nickname" )]
        public string NickName { get; set; }

        [JsonProperty( propertyName: "middle_name" )]
        public string MiddleName { get; set; }

        [JsonProperty( propertyName: "last_name" )]
        public string LastName { get; set; }

        [JsonProperty( propertyName: "birthdate" )]
        public DateTime? Birthdate { get; set; }

        [JsonProperty( propertyName: "anniversary" )]
        public DateTime? Anniversary { get; set; }

        [JsonProperty( propertyName: "grade" )]
        public int? Grade { get; set; }

        [JsonProperty( propertyName: "child" )]
        public bool Child { get; set; }

        [JsonProperty( propertyName: "graduation_year" )]
        public int? GraduationYear { get; set; }

        [JsonProperty( propertyName: "site_administrator" )]
        public bool SiteAdministrator { get; set; }

        [JsonProperty( propertyName: "accounting_administrator" )]
        public bool AccountingAdministrator { get; set; }

        [JsonProperty( propertyName: "people_permissions" )]
        public string PeoplePermissions { get; set; }

        [JsonProperty( propertyName: "membership" )]
        public string Membership { get; set; }

        [JsonProperty( propertyName: "inactivated_at" )]
        public DateTime? InactivedAt { get; set; }

        [JsonProperty( propertyName: "status" )]
        public string Status { get; set; }

        [JsonProperty( propertyName: "medical_notes" )]
        public string MedicalNotes { get; set; }

        [JsonProperty( propertyName: "mfa_configured" )]
        public bool? MfaConfigured { get; set; } = null;

        [JsonProperty( propertyName: "created_at" )]
        public DateTime? CreatedAt { get; set; } = null;

        [JsonProperty( propertyName: "updated_at" )]
        public DateTime? UpdatedAt { get; set; } = null;

        [JsonProperty( propertyName: "avatar" )]
        public string Avatar { get; set; }

        [JsonProperty( propertyName: "name" )]
        public string Name { get; set; }

        [JsonProperty( propertyName: "demographic_avatar_url" )]
        public string DemographicAvatarUrl { get; set; }

        [JsonProperty( propertyName: "directory_status" )]
        public string DirectoryStatus { get; set; }

        [JsonProperty( propertyName: "can_create_forms" )]
        public bool? CanCreateForms { get; set; } = null;

        [JsonProperty( propertyName: "can_email_lists" )]
        public bool? CanEmailLists { get; set; } = null;

        [JsonProperty( propertyName: "passed_background_check" )]
        public bool? PassedBackgroundCheck { get; set; } = null;

        [JsonProperty( propertyName: "school_type" )]
        public string SchoolType { get; set; }

        [JsonProperty( propertyName: "remote_id" )]
        public int? RemoteId { get; set; }

        [JsonProperty( propertyName: "marital_status" )]
        public MaritalStatus MaritalStatus { get; set; }
        [JsonProperty( propertyName: "name_prefix" )]
        public NamePrefix NamePrefix { get; set; }
        [JsonProperty( propertyName: "name_suffix" )]
        public NameSuffix NameSuffix { get; set; }
        public ICollection<Email> Emails { get; set; }
        public ICollection<Address> Addresses { get; set; }
        [JsonProperty( propertyName: "phone_numbers" )]
        public ICollection<PhoneNumber> PhoneNumbers { get; set; }

        public Stream AvatarUploadStream { get; set; }
    }
}
