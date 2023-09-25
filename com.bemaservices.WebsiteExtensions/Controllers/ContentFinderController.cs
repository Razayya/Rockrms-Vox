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
using com.bemaservices.WebsiteExtensions.Utility;
using com.bemaservices.WebsiteExtensions.Model;

namespace com.bemaservices.WebsiteExtensions.Controllers
{
    /// <summary>
    /// The controller class for the GroupTools
    /// </summary>
    public partial class ContentFinderController : Rock.Rest.ApiController<Rock.Model.ContentChannelItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupToolsController"/> class.
        /// </summary>
        public ContentFinderController() : base( new Rock.Model.ContentChannelItemService( new Rock.Data.RockContext() ) ) { }
    }

    public partial class ContentFinderController
    {
        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/WebsiteExtensions/GetContentInformation" )]
        public IQueryable<ContentInformation> GetContentInformation(
            string contentChannelItemIds = "",
            string contentChannelIds = "",
            string contentChannelTypeIds = "",
            bool showApprovedItems = true,
            bool showPendingItems = false,
            bool showDeniedItems = false,
            string titleKeywords = "",
            string contentKeywords = "",
            string attributeFilters = "",
            string requestedAttributeIds = "",
            int? offset = null,
            int? limit = null )
        {
            var rockContext = new RockContext();
            var contentInfoList = new List<ContentInformation>();
            //var definedValueService = new DefinedValueService( rockContext );
            int entityTypeId = EntityTypeCache.GetId( typeof( ContentChannelItem ) ) ?? 0;

            IQueryable<ContentChannelItem> qry = FilterContent(
                            new RockContext(),
                            contentChannelItemIds,
                            contentChannelIds,
                            contentChannelTypeIds,
                            showApprovedItems,
                            showPendingItems,
                            showDeniedItems,
                            titleKeywords,
                            contentKeywords,
                            attributeFilters );

            if ( offset.HasValue )
            {
                qry = qry.Skip( offset.Value );
            }

            if ( limit.HasValue )
            {
                qry = qry.Take( limit.Value );
            }

            var requestedAttributeIdList = requestedAttributeIds.SplitDelimitedValues().AsIntegerList();
            Dictionary<string, AttributeInformation> requestedAttributeList = new Dictionary<string, AttributeInformation>();
            Dictionary<string, AttributeCache> attributeCacheList = new Dictionary<string, AttributeCache>();
            foreach ( var requestedAttributeId in requestedAttributeIdList )
            {
                var requestedAttribute = WebsiteExtensionUtilities.GetAttributeInformation( requestedAttributeId );
                requestedAttributeList.AddOrReplace( requestedAttribute.Key, requestedAttribute );
                attributeCacheList.AddOrReplace( requestedAttribute.Key, AttributeCache.Get( requestedAttribute.Id ) );
            }

            foreach ( var contentChannelItem in qry.ToList() )
            {
                var contentInfo = new ContentInformation();
                contentInfo.Id = contentChannelItem.Id;
                contentInfo.Guid = contentChannelItem.Guid;
                contentInfo.ContentChannelId = contentChannelItem.ContentChannelId;
                contentInfo.ContentChannelTypeId = contentChannelItem.ContentChannelTypeId;
                contentInfo.Title = contentChannelItem.Title;
                contentInfo.Content = contentChannelItem.Content;
                contentInfo.StartDateTime = contentChannelItem.StartDateTime;
                contentInfo.ExpireDateTime = contentChannelItem.ExpireDateTime;

                contentChannelItem.LoadAttributes();
                contentInfo.AttributeValues = new Dictionary<string, AttributeValueInformation>();
                foreach ( var requestedAttribute in requestedAttributeList )
                {
                    var attributeCache = attributeCacheList[requestedAttribute.Key];
                    var field = attributeCache.FieldType.Field;

                    var attributeValue = new AttributeValueInformation();
                    attributeValue.Attribute = requestedAttribute.Value;
                    attributeValue.AttributeId = requestedAttribute.Value.Id;
                    attributeValue.AttributeKey = requestedAttribute.Value.Key;

                    attributeValue.RawValue = contentChannelItem.GetAttributeValue( attributeValue.AttributeKey );
                    attributeValue.FormattedValue = field.FormatValue( null, attributeCache.EntityTypeId, contentChannelItem.Id, attributeValue.RawValue, attributeCache.QualifierValues, false );

                    contentInfo.AttributeValues.Add( requestedAttribute.Key, attributeValue );
                }

                contentInfoList.Add( contentInfo );
            }

            var contentInfoQry = contentInfoList.AsQueryable()
                .OrderByDescending( cci => cci.StartDateTime )
                .ThenByDescending( cci => cci.Title )
                .AsQueryable();

            return contentInfoQry;
        }

        /// <summary>
        /// Gets the group count.
        /// </summary>
        /// <param name="groupTypeIds">The group type ids.</param>
        /// <param name="campusIds">The campus ids.</param>
        /// <param name="meetingDays">The meeting days.</param>
        /// <param name="categoryIds">The category ids.</param>
        /// <param name="lifeStageIds">The life stage ids.</param>
        /// <returns></returns>
        [Authenticate, Secured]
        [System.Web.Http.Route( "api/com_bemaservices/WebsiteExtensions/GetContentCount" )]
        public int GetContentCount(
            string contentChannelItemIds = "",
            string contentChannelIds = "",
            string contentChannelTypeIds = "",
            bool showApprovedItems = true,
            bool showPendingItems = false,
            bool showDeniedItems = false,
            string titleKeywords = "",
            string contentKeywords = "",
            string attributeFilters = "" )
        {
            IQueryable<ContentChannelItem> qry = FilterContent(
                new RockContext(),
                contentChannelItemIds,
                contentChannelIds,
                contentChannelTypeIds,
                showApprovedItems,
                showPendingItems,
                showDeniedItems,
                titleKeywords,
                contentKeywords,
                attributeFilters );

            return qry.Count();
        }

        private static IQueryable<ContentChannelItem> FilterContent(
            RockContext rockContext,
            string contentChannelItemIds = "",
            string contentChannelIds = "",
            string contentChannelTypeIds = "",
            bool showApprovedItems = true,
            bool showPendingItems = false,
            bool showDeniedItems = false,
            string titleKeywords = "",
            string contentKeywords = "",
            string attributeFilters = "" )
        {
            var contentChannelItemService = new ContentChannelItemService( rockContext );
            var qry = contentChannelItemService.Queryable()
                .AsNoTracking();

            var contentChannelItemIdList = contentChannelItemIds.SplitDelimitedValues().AsIntegerList();
            var contentChannelIdList = contentChannelIds.SplitDelimitedValues().AsIntegerList();
            var contentChannelTypeIdList = contentChannelTypeIds.SplitDelimitedValues().AsIntegerList();
            var attributeFilterList = WebsiteExtensionUtilities.GetAttributeFilters( attributeFilters );

            if ( contentChannelItemIdList.Any() )
            {
                qry = qry.Where( cci => contentChannelItemIdList.Contains( cci.Id ) );
            }

            if ( contentChannelIdList.Any() )
            {
                qry = qry.Where( cci => contentChannelIdList.Contains( cci.ContentChannelId ) );
            }

            if ( contentChannelTypeIdList.Any() )
            {
                qry = qry.Where( cci => contentChannelTypeIdList.Contains( cci.ContentChannelTypeId ) );
            }

            if ( titleKeywords.IsNotNullOrWhiteSpace() )
            {
                qry = qry.Where( cci => cci.Title.Contains( titleKeywords ) );
            }

            if ( contentKeywords.IsNotNullOrWhiteSpace() )
            {
                qry = qry.Where( cci => cci.Content.Contains( contentKeywords ) );
            }

            if ( contentChannelItemIdList.Any() )
            {
                qry = qry.Where( cci => contentChannelItemIdList.Contains( cci.Id ) );
            }

            qry = qry.Where( cci => cci.ContentChannel.ContentChannelType.DateRangeType == ContentChannelDateType.NoDates || cci.StartDateTime <= RockDateTime.Now );

            qry = qry.Where( cci =>
                    cci.ContentChannel.ContentChannelType.DateRangeType != ContentChannelDateType.DateRange ||
                    !cci.ExpireDateTime.HasValue ||
                    ( cci.ContentChannel.ContentChannelType.IncludeTime && cci.ExpireDateTime >= RockDateTime.Now ) ||
                    ( !cci.ContentChannel.ContentChannelType.IncludeTime && cci.ExpireDateTime >= RockDateTime.Today )
                );

            qry = qry.Where( cci =>
                     ( showApprovedItems && cci.Status == ContentChannelItemStatus.Approved ) ||
                     ( showPendingItems && cci.Status == ContentChannelItemStatus.PendingApproval ) ||
                     ( showDeniedItems && cci.Status == ContentChannelItemStatus.Denied )
                );

            foreach ( var attributeFilter in attributeFilterList )
            {
                qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == attributeFilter.AttributeKey && attributeFilter.FilterValues.Any( c => av.Value.ToUpper().Contains( c ) ) );
            }


            return qry
                .OrderByDescending( cci => cci.StartDateTime )
                .ThenByDescending( cci => cci.Title );
        }
    }

    /// <summary>
    /// A class to store content data to be returned by the API
    /// </summary>
    public class ContentInformation
    {
        public int Id { get; set; }

        public Guid Guid { get; set; }

        public int ContentChannelId { get; set; }

        public int ContentChannelTypeId { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public DateTime? StartDateTime { get; set; }

        public DateTime? ExpireDateTime { get; set; }

        public Dictionary<string, AttributeValueInformation> AttributeValues { get; set; }
    }

}
