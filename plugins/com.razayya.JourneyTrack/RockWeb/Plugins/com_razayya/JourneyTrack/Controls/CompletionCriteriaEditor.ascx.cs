using System;
using System.Collections.Generic;
using System.Data.Entity;
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

        /// <summary>
        /// True once this editor has been seeded (or interacted with) at least
        /// once in the current page lifetime. The detail block uses this to
        /// avoid re-seeding (and wiping) the editor on calc-type toggles.
        /// </summary>
        public bool HasInSessionState
        {
            get { return ( ViewState["CCE_HasState"] as bool? ) ?? false; }
            private set { ViewState["CCE_HasState"] = value; }
        }

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
            HasInSessionState = true;
            BindRepeater();
        }

        private string GetJson()
        {
            CaptureRowsToState();
            return JsonConvert.SerializeObject( Criteria );
        }

        /// <summary>
        /// Returns user-facing validation errors. Empty list means OK to save.
        /// An empty Criteria list is considered valid (calc matches nobody).
        /// </summary>
        public List<string> GetValidationErrors()
        {
            CaptureRowsToState();
            var errors = new List<string>();
            var list = Criteria;
            for ( int i = 0; i < list.Count; i++ )
            {
                var c = list[i];
                var prefix = "Completion row " + ( i + 1 );

                if ( c.JourneyCalculationId <= 0 )
                {
                    errors.Add( prefix + ": Calculation must be selected." );
                }

                bool needsValue = c.Comparison != ComparisonType.IsBlank
                               && c.Comparison != ComparisonType.IsNotBlank;
                if ( needsValue && string.IsNullOrEmpty( c.Value ) )
                {
                    errors.Add( prefix + ": Value is required for comparison '" + SplitCamelCase( c.Comparison.ToString() ) + "'." );
                }
            }
            return errors;
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
            HasInSessionState = true;
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

        #region View-mode summary formatter

        /// <summary>
        /// Render the configured CompletionCriteria JSON as a friendly inline
        /// summary in DataView-filter style:
        ///   <strong>StepCompletion: Begin</strong> is not blank <strong>AND</strong>
        ///   <strong>Choose Campus</strong> equal to <code>"True"</code>
        /// Required criteria are AND-joined. Non-required criteria show "(optional)".
        /// Stage Id is needed to resolve sibling calc names.
        /// </summary>
        public static string FormatSummaryHtml( string criteriaJson, int stageId, int excludeCalcId )
        {
            List<CompletionCriterion> list;
            try
            {
                list = JsonConvert.DeserializeObject<List<CompletionCriterion>>( criteriaJson ?? "[]" )
                    ?? new List<CompletionCriterion>();
            }
            catch
            {
                return "<em class='text-muted'>(invalid JSON)</em>";
            }

            if ( list.Count == 0 )
            {
                return "<em class='text-muted'>(no criteria configured — matches nobody)</em>";
            }

            Dictionary<int, string> calcNames;
            using ( var rockContext = new RockContext() )
            {
                calcNames = new JourneyCalculationService( rockContext ).Queryable().AsNoTracking()
                    .Where( c => c.StageId == stageId && c.Id != excludeCalcId )
                    .Select( c => new { c.Id, c.Name } )
                    .ToDictionary( c => c.Id, c => c.Name );
            }

            var sb = new System.Text.StringBuilder();
            for ( int i = 0; i < list.Count; i++ )
            {
                if ( i > 0 )
                {
                    sb.Append( "<div class='text-muted small'>AND</div>" );
                }
                sb.Append( "<div>" ).Append( FormatCriterionLine( list[i], calcNames ) ).Append( "</div>" );
            }
            return sb.ToString();
        }

        private static string FormatCriterionLine( CompletionCriterion c, Dictionary<int, string> calcNames )
        {
            var name = calcNames.TryGetValue( c.JourneyCalculationId, out var n ) ? n : ( "calc #" + c.JourneyCalculationId );
            var keyLabel = System.Web.HttpUtility.HtmlEncode( name );
            var optionalSuffix = c.IsRequired ? string.Empty : " <span class='label label-default'>optional</span>";

            string cmpText;
            switch ( c.Comparison )
            {
                case ComparisonType.EqualTo:               cmpText = "equal to"; break;
                case ComparisonType.NotEqualTo:            cmpText = "not equal to"; break;
                case ComparisonType.IsBlank:               cmpText = "is blank"; break;
                case ComparisonType.IsNotBlank:            cmpText = "is not blank"; break;
                case ComparisonType.Contains:              cmpText = "contains"; break;
                case ComparisonType.GreaterThan:           cmpText = "greater than"; break;
                case ComparisonType.LessThan:              cmpText = "less than"; break;
                case ComparisonType.GreaterThanOrEqualTo:  cmpText = "&ge;"; break;
                case ComparisonType.LessThanOrEqualTo:     cmpText = "&le;"; break;
                default:                                   cmpText = SplitCamelCase( c.Comparison.ToString() ); break;
            }

            if ( c.Comparison == ComparisonType.IsBlank || c.Comparison == ComparisonType.IsNotBlank )
            {
                return "<strong>" + keyLabel + "</strong> <span class='text-muted'>" + cmpText + "</span>" + optionalSuffix;
            }
            return "<strong>" + keyLabel + "</strong> "
                + "<span class='text-muted'>" + cmpText + "</span> "
                + System.Web.HttpUtility.HtmlEncode( c.Value ?? string.Empty )
                + optionalSuffix;
        }

        #endregion
    }
}
