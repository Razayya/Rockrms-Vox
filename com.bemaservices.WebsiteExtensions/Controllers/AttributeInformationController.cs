// <copyright>
// Copyright by BEMA Information Technologies
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
using System.Data.Entity;
using System.Drawing;
using System.Linq;
using DDay.iCal;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Rest.Filters;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using com.bemaservices.WebsiteExtensions.Model;
using com.bemaservices.WebsiteExtensions.Utility;

namespace com.bemaservices.WebsiteExtensions.Controllers
{
    /// <summary>
    /// The controller class for the GroupTools
    /// </summary>
    public partial class AttributeInformationController : Rock.Rest.ApiController<Rock.Model.ContentChannelItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupToolsController"/> class.
        /// </summary>
        public AttributeInformationController() : base( new Rock.Model.ContentChannelItemService( new Rock.Data.RockContext() ) ) { }
    }

    public partial class AttributeInformationController
    {
        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/WebsiteExtensions/GetAttributeInformation" )]
        public AttributeInformation GetAttributeInformation(
          int? attributeId = null )
        {
            var attributeInformation = new AttributeInformation();

            if ( attributeId.HasValue )
            {
                attributeInformation = WebsiteExtensionUtilities.GetAttributeInformation( attributeId.Value );
            }

            return attributeInformation;
        }

        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/WebsiteExtensions/GetAttributeInformationList" )]
        public List<AttributeInformation> GetAttributesInformation(
          string attributeIds = "" )
        {
            var attributeInformationList = new List<AttributeInformation>();

            var attributeIdList = attributeIds.SplitDelimitedValues().AsIntegerList();
            foreach ( var attributeId in attributeIdList )
            {
                var attributeInformation = WebsiteExtensionUtilities.GetAttributeInformation( attributeId );
                attributeInformationList.Add( attributeInformation );
            }

            return attributeInformationList;
        }

    }
}
