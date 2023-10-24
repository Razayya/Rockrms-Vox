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
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Lava;
using Rock.Model;
using Rock.Security;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using com.bemaservices.RoomManagement.Model;
using RestSharp.Extensions;
using com.blueboxmoon.ProjectManagement.Model;

namespace RockWeb.Plugins.com_razayya.Blocks.ProjectManagement
{
    /// <summary>
    /// A wizard to simplify creation of Event Registrations.
    /// </summary>
    [DisplayName( "Project Detail Project Board List" )]
    [Category( "Razayya > Project Management" )]
    [Description( "A Block that shows all the Project Boards and allows users to toggle which board a Project is shown on." )]

    [ContextAware( typeof( Project ) )]
    public partial class ProjectDetailProjectBoardList : RockBlock, ISecondaryBlock
    {

        #region Control Methods

        /// <summary>
        /// Bind the repeater to the list of projects in the project.
        /// </summary>
        private void BindRepeater()
        {
            using ( var rockContext = new RockContext() )
            {
                var boards = new ProjectBoardService( rockContext ).Queryable()
                    .OrderBy( a => a.Name )
                    .ToList()
                    .Where( a => a.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                    .ToList();

                rpProjectBoard.DataSource = boards;
            }
        }

        #region Control Initialization

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            this.BlockUpdated += Block_BlockUpdated;

            if ( !Page.IsPostBack )
            {
                BindRepeater();
            }            
        }

        #endregion Control Initialization

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
        }

        #endregion Control Methods

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            // reload page if block settings where changed
        }

        public void SetVisible( bool visible )
        {
            pnlProjectBoardList.Visible = visible;
        }

        /// <summary>
        /// Handles the ItemDataBound event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterItemEventArgs"/> instance containing the event data.</param>
        protected void rpProjectBoard_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            var project = e.Item.DataItem as Project;
            var liProjectBoard = e.Item.FindControl( "liProjectBoard" ) as System.Web.UI.HtmlControls.HtmlGenericControl;
        }

        /// <summary>
        /// Handles the ItemCommand event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void rpProjectBoard_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "ToggleProjectBoard" )
            {

                var project = ContextEntity<Project>();

                if( project != null )
                {
                    var projectBoardId = e.CommandArgument.ToString().AsIntegerOrNull();
                    if( projectBoardId.HasValue )
                    {
                        var defaultColumn = new ProjectBoardColumnService( new RockContext() ).Queryable().Where( c => c.ProjectBoardId == projectBoardId.Value ).SortBy( "Order" ).FirstOrDefault();
                        var card = ProjectBoardCardService.InsertProjectCardAndSort( project.Id, projectBoardId.Value, defaultColumn.Id );
                    }
                }
               
                return;
            }

            BindRepeater();
        }
    }
}