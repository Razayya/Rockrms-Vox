using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanningCenterSDK.Apps.Services.Model
{
    internal class RenderEmailTemplateRequest
    {
        public string Format { get; set; }
        public Person Person { get; set; }
    }
}
