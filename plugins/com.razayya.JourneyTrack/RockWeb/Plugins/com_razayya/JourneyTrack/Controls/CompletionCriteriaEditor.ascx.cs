using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;
using ComparisonType = com.razayya.JourneyTrack.Model.ComparisonType;

using Newtonsoft.Json;

using Rock;
using Rock.Data;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    public partial class CompletionCriteriaEditor : UserControl
    {
        #region Public API

        public string Value
        {
            get { return GetJson(); }
            set { SetJson( value ); }
        }

        /// <summary>
        /// The Stage whose sibling JourneyCalculations populate the dropdown.
        /// Set this BEFORE assigning Value / calling SetJson.
        /// </summary>
        public int StageId
        {
            get { return ( ViewState["StageId"] as int? ) ?? 0; }
            set { ViewState["StageId"] = value; }
        }

        /// <summary>
        /// Optional: id of the calculation currently being edited (excluded from sibling dropdown).
        /// </summary>
        public int ExcludeCalculationId
        {
            get { return ( ViewState["ExcludeCalculationId"] as int? ) ?? 0; }
            set { ViewState["ExcludeCalculationId"] = value; }
        }

        #endregion

        #region ViewState

        private List<CompletionCriterion> Criteria
        {
            get
            {
                var json = ViewState["Criteria"] as string;
                if ( string.IsNullOrEmpty( json ) ) return new List<CompletionCriterion>();
                try { return JsonConvert.DeserializeObject<List<CompletionCriterion>>( json ) ?? new List<CompletionCriterion>(); }
                catch { return new List<CompletionCriterion>(); }
            }
            set { ViewState["Criteria"] = JsonConvert.SerializeObject( value ?? new List<CompletionCriterion>() ); }
        }

        // Cached per-postback sibling list. Re-resolves StageId → siblings each time the editor binds.
        private List<JourneyCalculation> _siblings;
        private List<JourneyCalculation> GetSiblings()
        {
            if ( _siblings != null ) return _siblings;
            if ( StageId <= 0 ) return _siblings = new List<JourneyCalculation>();

            using ( var rockContext = new RockContext() )
            {
                _siblings = new JourneyCalculationService( rockContext ).Queryable()
                    .Where( c => c.StageId == StageId && c.Id != ExcludeCalculationId )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .ToList();
            }
            return _siblings;
        }

        #endregion

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );
            if ( !Page.IsPostBack )
            {
                BindRepeater();
            }
        }

        #region Public Get/Set JSON

        private void SetJson( string json )
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<CompletionCriterion>>( json ?? "[]" )
                    ?? new List<CompletionCriterion>();
                Criteria = list;
            }
            catch
            {
                Criteria = new List<CompletionCriterion>();
            }
            BindRepeater();
        }

        private string GetJson()
        {
            CaptureRowsToState();
            return JsonConvert.SerializeObject( Criteria );
        }

        #endregion

        #region Binding

        private void BindRepeater()
        {
            _siblings = null; // force refetch
            var siblings = GetSiblings();
            nbNoSiblings.Visible = siblings.Count == 0;
            lbAddRow.Enabled = siblings.Count > 0;

            var list = Criteria;
            phNoRows.Visible = list.Count == 0;
            rRows.DataSource = list;
            rRows.DataBind();
        }

        protected void rRows_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem )
            {
                return;
            }

            var criterion = ( CompletionCriterion ) e.Item.DataItem;
            BindRow( e.Item, criterion );
        }

        private void BindRow( RepeaterItem item, CompletionCriterion criterion )
        {
            var ddlCalc  = ( RockDropDownList ) item.FindControl( "ddlCalc" );
            var cbReq    = ( RockCheckBox ) item.FindControl( "cbRequired" );
            var ddlComp  = ( RockDropDownList ) item.FindControl( "ddlComparison" );
            var tbVal    = ( RockTextBox ) item.FindControl( "tbValue" );

            // Sibling dropdown
            ddlCalc.Items.Clear();
            ddlCalc.Items.Add( new ListItem( "(select)", string.Empty ) );
            foreach ( var sib in GetSiblings() )
            {
                ddlCalc.Items.Add( new ListItem( sib.Name, sib.Id.ToString() ) );
            }
            ddlCalc.SetValue( criterion.JourneyCalculationId.ToString() );

            // Comparison
            ddlComp.Items.Clear();
            foreach ( var ct in Enum.GetValues( typeof( ComparisonType ) ).Cast<ComparisonType>() )
            {
                // Completion doesn't support GreaterThanOrEqualTo / LessThanOrEqualTo / Contains in the same way;
                // expose the same set as CompareValues handles uniformly (text/date/numeric coercion).
                ddlComp.Items.Add( new ListItem( SplitCamelCase( ct.ToString() ), ct.ToString() ) );
            }
            ddlComp.SetValue( criterion.Comparison.ToString() );

            // Required + Value
            cbReq.Checked = criterion.IsRequired;
            bool needsValue = criterion.Comparison != ComparisonType.IsBlank
                           && criterion.Comparison != ComparisonType.IsNotBlank;
            tbVal.Visible = needsValue;
            tbVal.Text = needsValue ? ( criterion.Value ?? string.Empty ) : string.Empty;
        }

        private void CaptureRowsToState()
        {
            var list = new List<CompletionCriterion>();
            foreach ( RepeaterItem item in rRows.Items )
            {
                if ( item.ItemType != ListItemType.Item && item.ItemType != ListItemType.AlternatingItem )
                {
                    continue;
                }

                var ddlCalc = ( RockDropDownList ) item.FindControl( "ddlCalc" );
                var cbReq   = ( RockCheckBox ) item.FindControl( "cbRequired" );
                var ddlComp = ( RockDropDownList ) item.FindControl( "ddlComparison" );
                var tbVal   = ( RockTextBox ) item.FindControl( "tbValue" );

                var comp = ddlComp.SelectedValue.ConvertToEnumOrNull<ComparisonType>() ?? ComparisonType.EqualTo;
                list.Add( new CompletionCriterion
                {
                    JourneyCalculationId = ddlCalc.SelectedValue.AsInteger(),
                    IsRequired = cbReq.Checked,
                    Comparison = comp,
                    Value = ( comp == ComparisonType.IsBlank || comp == ComparisonType.IsNotBlank )
                        ? string.Empty
                        : ( tbVal.Text ?? string.Empty )
                } );
            }
            Criteria = list;
        }

        #endregion

        #region Postback Handlers

        protected void ddlComparison_SelectedIndexChanged( object sender, EventArgs e )
        {
            CaptureRowsToState();
            BindRepeater();
        }

        protected void lbAddRow_Click( object sender, EventArgs e )
        {
            CaptureRowsToState();
            var list = Criteria;
            list.Add( new CompletionCriterion
            {
                JourneyCalculationId = 0,
                IsRequired = true,
                Comparison = ComparisonType.IsNotBlank,
                Value = string.Empty
            } );
            Criteria = list;
            BindRepeater();
        }

        protected void rRows_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName != "DeleteRow" ) return;
            CaptureRowsToState();
            var index = e.CommandArgument.ToString().AsInteger();
            var list = Criteria;
            if ( index >= 0 && index < list.Count )
            {
                list.RemoveAt( index );
                Criteria = list;
            }
            BindRepeater();
        }

        #endregion

        private static string SplitCamelCase( string s )
        {
            if ( string.IsNullOrEmpty( s ) ) return s;
            return System.Text.RegularExpressions.Regex.Replace( s, "(?<=[a-z])([A-Z])", " $1" );
        }
    }
}
