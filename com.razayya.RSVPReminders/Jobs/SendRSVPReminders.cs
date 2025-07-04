// <copyright>
// Copyright by BEMA Software Services
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;

using Quartz;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace com.razayya.RSVPReminders.Jobs
{

    [DisplayName("Send RSVP Reminders")]
    [Description("Handles RSVP communications as well as populating Group RSVP details.")]

    [DisallowConcurrentExecution]
    public class SendRSVPReminders : RockJob
    {

        public SendRSVPReminders()
        {
        }

        public override void Execute()
        {
            try
            {
                var rockContext = new RockContext();
            }

            catch (Exception ex)
            {
                ExceptionLogService.LogException(ex, null);
            }
            

            var resultMsg = new StringBuilder();


            this.Result = "";
        }
    }
}