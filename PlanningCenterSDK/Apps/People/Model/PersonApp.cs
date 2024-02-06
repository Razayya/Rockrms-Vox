using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanningCenterSDK.Apps.People.Model
{
    public class PersonApp
    {
        public string Id { get; set; }
        public string AllowPcoLogin { get; set; }
        public string PeoplePermissions { get; set; }
        public App App { get; set; }
        public Person Person { get; set; }
    }
}
