// <copyright>
// Copyright by Razayya Financial 
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
using System.IO;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;
using Rock.Web.UI;

using PlanningCenterSDK.Apps.Services;
using Azure.Storage.Blobs.Models;
using com.razayya.PCOTeamSync;

namespace RockWeb.Plugins.com_razayya.Blocks.PCOSync
{
    [DisplayName( "Team List" )]
    [Category( "Razayya > PCO Sync" )]
    [Description( "Template block for developers to use to start a new list block." )]
    public partial class TeamList : RockBlock, ICustomGridColumns
    {
        #region Fields

        // used for private variables

        #endregion

        #region Properties

        // used for public / protected properties

        #endregion

        #region Base Control Methods

        //  overrides of the base RockBlock methods (i.e. OnInit, OnLoad)

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            gList.GridRebind += gList_GridRebind;

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                BindGrid();
            }
        }

        #endregion

        #region Events

        // handlers called by the controls on your block

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {

        }

        /// <summary>
        /// Handles the GridRebind event of the gList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gList_GridRebind( object sender, EventArgs e )
        {
            BindGrid();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds the grid.
        /// </summary>
        private void BindGrid()
        {
            using ( var rockContext = new RockContext() )
            {
                var definedValueService = new DefinedValueService( rockContext );
                var definedPcoAccounts = definedValueService.GetByDefinedTypeGuid( com.razayya.PCOTeamSync.SystemGuid.DefinedType.PCO_ACCOUNTS.AsGuid() ).ToList();

                foreach( var pcoAccount in definedPcoAccounts )
                {

                    pcoAccount.LoadAttributes();

                    var applicationId = pcoAccount.GetAttributeValue( AttributeCache.Get( com.razayya.PCOTeamSync.SystemGuid.Attribute.PCO_ACCOUNT_APPLICATION_ID.AsGuid() ).Key );
                    var secret = pcoAccount.GetAttributeValue( AttributeCache.Get( com.razayya.PCOTeamSync.SystemGuid.Attribute.PCO_ACCOUNT_SECRET.AsGuid() ).Key );

                    var pcoServiceApp = ServicesApp.GetInstance( applicationId, secret );

                    var parameters = new Dictionary<string, string>() { { "include", "service_type" } };

                    var pcoTeamSync = new PCOTeamSync( applicationId, secret );

                    //pcoTeamSync.SyncPeopleData();
                    //pcoTeamSync.ImportServiceTypes();
                    //pcoTeamSync.SyncTeamsAndPositions();

                    var teams = pcoServiceApp.Team.GetAsync( 0, 25, parameters ).GetAwaiter().GetResult();
                    
                    gList.SetLinqDataSource( teams.AsQueryable() );
                    gList.DataBind();
                }
            }
        }
        #endregion
    }
}