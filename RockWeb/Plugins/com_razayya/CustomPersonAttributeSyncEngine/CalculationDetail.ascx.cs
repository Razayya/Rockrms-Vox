using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes;
using com.razayya.CustomPersonAttributeSyncEngine.Model;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

using NoMatchBehavior = com.razayya.CustomPersonAttributeSyncEngine.Model.NoMatchBehavior;

namespace RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine
{
    [DisplayName( "Calculation Detail" )]
    [Category( "Razayya > Attribute Sync Engine" )]
    [Description( "Displays details for a single Calculation with component-specific configuration." )]

    [LinkedPage( "Parent Page",
        Description = "Page to navigate back to the parent Sub Group.",
        IsRequired = false,
        Order = 0,
        Key = "ParentPage" )]

    public partial class CalculationDetail : RockBlock
    {
        #region Properties

        private int CalculationId
        {
            get { return ViewState["CalculationId"] as int? ?? 0; }
            set { ViewState["CalculationId"] = value; }
        }

        private int ParentSubGroupId
        {
            get { return ViewState["ParentSubGroupId"] as int? ?? 0; }
            set { ViewState["ParentSubGroupId"] = value; }
        }

        #endregion

        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                int calcId = PageParameter( "CalculationId" ).AsInteger();
                int subGroupId = PageParameter( "CalculationSubGroupId" ).AsInteger();
                ParentSubGroupId = subGroupId;

                PopulateDropDowns();
                ShowDetail( calcId );
            }
        }

        #endregion

        #region Events

        protected void btnEdit_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var calc = new CalculationService( rockContext ).Get( CalculationId );
                ShowEditDetails( calc, rockContext );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new CalculationService( rockContext );
                Calculation calc;

                if ( CalculationId != 0 )
                {
                    calc = service.Get( CalculationId );
                }
                else
                {
                    calc = new Calculation();
                    calc.CalculationSubGroupId = ParentSubGroupId;
                    service.Add( calc );
                }

                calc.Name = tbName.Text;
                calc.Description = tbDescription.Text;
                calc.IsActive = cbIsActive.Checked;
                calc.PersonAttributeId = apTargetAttribute.SelectedValueAsInt() ?? 0;
                calc.ResultLavaTemplate = ceResultLava.Text;
                calc.NoMatchBehavior = ddlNoMatchBehavior.SelectedValue.AsInteger() == 1
                    ? NoMatchBehavior.WriteLava
                    : NoMatchBehavior.LeaveUnchanged;
                calc.NoMatchLavaTemplate = ceNoMatchLava.Text;

                // Resolve selected component to EntityTypeId
                var componentEntityTypeGuid = cpCalculationType.SelectedValue.AsGuidOrNull();
                if ( componentEntityTypeGuid.HasValue )
                {
                    var entityType = EntityTypeCache.Get( componentEntityTypeGuid.Value );
                    if ( entityType != null )
                    {
                        calc.CalculationTypeEntityTypeId = entityType.Id;
                    }
                }

                if ( !calc.IsValid )
                {
                    return;
                }

                rockContext.SaveChanges();

                // Save component-specific attributes
                calc.LoadAttributes( rockContext );
                Rock.Attribute.Helper.GetEditValues( phComponentAttributes, calc );
                calc.SaveAttributeValues( rockContext );

                CalculationId = calc.Id;
                ParentSubGroupId = calc.CalculationSubGroupId;
            }

            ShowDetail( CalculationId );
        }

        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( CalculationId == 0 )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowDetail( CalculationId );
            }
        }

        protected void btnBack_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "ParentPage", "CalculationSubGroupId", ParentSubGroupId );
        }

        protected void cpCalculationType_SelectedIndexChanged( object sender, EventArgs e )
        {
            LoadComponentAttributes();
        }

        protected void ddlNoMatchBehavior_SelectedIndexChanged( object sender, EventArgs e )
        {
            pnlNoMatchLava.Visible = ddlNoMatchBehavior.SelectedValue == "1";
        }

        protected void btnPlay_Click( object sender, EventArgs e )
        {
            RunPreview();
        }

        protected void btnExecutePreview_Click( object sender, EventArgs e )
        {
            ExecuteCalculation();
        }

        protected void btnClosePreview_Click( object sender, EventArgs e )
        {
            pnlPreview.Visible = false;
            pnlView.Visible = true;
        }

        #endregion

        #region Methods

        private void PopulateDropDowns()
        {
            ddlNoMatchBehavior.Items.Clear();
            ddlNoMatchBehavior.Items.Add( new System.Web.UI.WebControls.ListItem( "Leave Unchanged", "0" ) );
            ddlNoMatchBehavior.Items.Add( new System.Web.UI.WebControls.ListItem( "Write Lava Value", "1" ) );
        }

        private void ShowDetail( int calcId )
        {
            pnlDetails.Visible = true;
            pnlPreview.Visible = false;

            Calculation calc = null;

            if ( calcId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    calc = new CalculationService( rockContext ).Queryable()
                        .Include( c => c.CalculationTypeEntityType )
                        .Include( c => c.PersonAttribute )
                        .Include( c => c.CalculationSubGroup )
                        .FirstOrDefault( c => c.Id == calcId );
                }
            }

            if ( calc == null )
            {
                calc = new Calculation { IsActive = true, CalculationSubGroupId = ParentSubGroupId };
                lTitle.Text = ActionTitle.Add( "Calculation" ).FormatAsHtmlTitle();
                using ( var rockContext = new RockContext() )
                {
                    ShowEditDetails( calc, rockContext );
                }
                return;
            }

            CalculationId = calc.Id;
            ParentSubGroupId = calc.CalculationSubGroupId;

            lTitle.Text = calc.Name.FormatAsHtmlTitle();
            hlInactive.Visible = !calc.IsActive;
            hlCalcType.Text = calc.CalculationTypeEntityType?.FriendlyName ?? "Unknown";

            lViewDescription.Text = calc.Description;

            string details = string.Empty;
            details += string.Format( "<dt>Target Attribute</dt><dd>{0}</dd>", calc.PersonAttribute?.Name ?? "Unknown" );
            details += string.Format( "<dt>No Match Behavior</dt><dd>{0}</dd>",
                calc.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged ? "Leave Unchanged" : "Write Lava Value" );

            if ( !string.IsNullOrWhiteSpace( calc.ResultLavaTemplate ) )
            {
                details += string.Format( "<dt>Result Template</dt><dd><code>{0}</code></dd>",
                    System.Web.HttpUtility.HtmlEncode( calc.ResultLavaTemplate ) );
            }

            lViewDetails.Text = details;

            // Show component-specific attribute values in view mode
            using ( var rockContext = new RockContext() )
            {
                calc.LoadAttributes( rockContext );
                string configHtml = string.Empty;
                foreach ( var attr in calc.Attributes )
                {
                    var value = calc.GetAttributeValue( attr.Key );
                    if ( !string.IsNullOrWhiteSpace( value ) )
                    {
                        var formattedValue = attr.Value.FieldType.Field.FormatValue( null, attr.Value.EntityTypeId, calc.Id, value, attr.Value.QualifierValues, false );
                        configHtml += string.Format( "<dt>{0}</dt><dd>{1}</dd>", attr.Value.Name, formattedValue );
                    }
                }
                lViewConfig.Text = configHtml;
            }

            pnlView.Visible = true;
            pnlEdit.Visible = false;
        }

        private void ShowEditDetails( Calculation calc, RockContext rockContext )
        {
            pnlView.Visible = false;
            pnlEdit.Visible = true;

            tbName.Text = calc.Name;
            tbDescription.Text = calc.Description;
            cbIsActive.Checked = calc.IsActive;

            // Set target attribute
            if ( calc.PersonAttributeId > 0 )
            {
                apTargetAttribute.SetValue( calc.PersonAttributeId );
            }

            // Set calculation type
            if ( calc.CalculationTypeEntityTypeId > 0 )
            {
                var entityType = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId );
                if ( entityType != null )
                {
                    cpCalculationType.SetValue( entityType.Guid.ToString() );
                }
            }

            // Result config
            ceResultLava.Text = calc.ResultLavaTemplate;
            ddlNoMatchBehavior.SetValue( (int)calc.NoMatchBehavior );
            pnlNoMatchLava.Visible = calc.NoMatchBehavior == NoMatchBehavior.WriteLava;
            ceNoMatchLava.Text = calc.NoMatchLavaTemplate;

            // Load component attributes
            LoadComponentAttributes( calc, rockContext );
        }

        private void LoadComponentAttributes( Calculation calc = null, RockContext rockContext = null )
        {
            phComponentAttributes.Controls.Clear();
            pnlComponentAttributes.Visible = false;
            pnlMergeFields.Visible = false;

            var componentEntityTypeGuid = cpCalculationType.SelectedValue.AsGuidOrNull();
            if ( !componentEntityTypeGuid.HasValue )
            {
                return;
            }

            var entityType = EntityTypeCache.Get( componentEntityTypeGuid.Value );
            if ( entityType == null )
            {
                return;
            }

            var component = CalculationTypeContainer.GetComponent( entityType.Name );
            if ( component == null )
            {
                return;
            }

            pnlComponentAttributes.Visible = true;
            lComponentName.Text = component.Title + " Settings";

            // Show merge field documentation
            var mergeFields = component.GetMergeFields();
            if ( mergeFields != null && mergeFields.Count > 0 )
            {
                pnlMergeFields.Visible = true;
                string mfHtml = "<table class='table table-condensed table-bordered'><thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead><tbody>";
                foreach ( var mf in mergeFields )
                {
                    mfHtml += string.Format( "<tr><td><code>{{{{ {0} }}}}</code></td><td>{1}</td><td>{2}</td></tr>",
                        mf.Name, mf.DataType, mf.Description );
                }
                mfHtml += "</tbody></table>";
                lMergeFields.Text = mfHtml;
            }

            // Load attributes for this calculation's component type
            bool disposeContext = rockContext == null;
            rockContext = rockContext ?? new RockContext();
            try
            {
                if ( calc == null && CalculationId > 0 )
                {
                    calc = new CalculationService( rockContext ).Get( CalculationId );
                }

                if ( calc == null )
                {
                    calc = new Calculation
                    {
                        CalculationTypeEntityTypeId = entityType.Id,
                        CalculationSubGroupId = ParentSubGroupId
                    };
                }

                calc.LoadAttributes( rockContext );
                Rock.Attribute.Helper.AddEditControls( calc, phComponentAttributes, true, BlockValidationGroup );
            }
            finally
            {
                if ( disposeContext )
                {
                    rockContext.Dispose();
                }
            }
        }

        private void RunPreview()
        {
            pnlView.Visible = false;
            pnlPreview.Visible = true;

            using ( var rockContext = new RockContext() )
            {
                var calc = new CalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Include( c => c.CalculationSubGroup.CalculationGroup )
                    .FirstOrDefault( c => c.Id == CalculationId );

                if ( calc == null )
                {
                    nbPreviewInfo.Text = "Calculation not found.";
                    nbPreviewInfo.NotificationBoxType = NotificationBoxType.Warning;
                    return;
                }

                // Build population (simplified — uses parent group's filters)
                var group = calc.CalculationSubGroup.CalculationGroup;
                var personQuery = new PersonService( rockContext ).Queryable().AsNoTracking();

                if ( group.RecordStatusValueId.HasValue )
                {
                    personQuery = personQuery.Where( p => p.RecordStatusValueId == group.RecordStatusValueId.Value );
                }
                if ( group.ConnectionStatusValueId.HasValue )
                {
                    personQuery = personQuery.Where( p => p.ConnectionStatusValueId == group.ConnectionStatusValueId.Value );
                }
                if ( group.CampusId.HasValue )
                {
                    personQuery = personQuery.Where( p => p.PrimaryCampusId == group.CampusId.Value );
                }

                var populationIds = new HashSet<int>( personQuery.Select( p => p.Id ).Take( 5000 ).ToList() );

                // Resolve component
                var entityType = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId );
                var component = CalculationTypeContainer.GetComponent( entityType?.Name );

                if ( component == null )
                {
                    nbPreviewInfo.Text = "Calculation type component not found.";
                    nbPreviewInfo.NotificationBoxType = NotificationBoxType.Warning;
                    return;
                }

                calc.LoadAttributes( rockContext );
                var matchedResults = component.Evaluate( rockContext, calc, populationIds );

                // Build preview data
                var targetAttribute = AttributeCache.Get( calc.PersonAttributeId );
                var previewData = new List<PreviewRow>();

                var samplePersonIds = matchedResults.Keys.Take( 100 ).ToList();
                // Also include some non-matches
                var nonMatchIds = populationIds.Where( id => !matchedResults.ContainsKey( id ) ).Take( 20 ).ToList();
                samplePersonIds.AddRange( nonMatchIds );

                var persons = new PersonService( rockContext ).Queryable().AsNoTracking()
                    .Where( p => samplePersonIds.Contains( p.Id ) )
                    .ToList();

                foreach ( var person in persons )
                {
                    person.LoadAttributes( rockContext );
                    var currentValue = person.GetAttributeValue( targetAttribute?.Key ?? string.Empty ) ?? string.Empty;

                    string newValue;
                    string action;

                    if ( matchedResults.TryGetValue( person.Id, out var mergeFields ) )
                    {
                        newValue = !string.IsNullOrWhiteSpace( calc.ResultLavaTemplate )
                            ? calc.ResultLavaTemplate.ResolveMergeFields( mergeFields )
                            : mergeFields.ContainsKey( "Matched" ) ? mergeFields["Matched"]?.ToString() : "True";
                        action = currentValue == newValue ? "No Change" : "Update";
                    }
                    else
                    {
                        newValue = calc.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged ? currentValue : "(Write No-Match Value)";
                        action = calc.NoMatchBehavior == NoMatchBehavior.LeaveUnchanged ? "Skip" : "Update";
                    }

                    previewData.Add( new PreviewRow
                    {
                        PersonName = person.FullName,
                        CurrentValue = currentValue,
                        NewValue = newValue,
                        Action = action
                    } );
                }

                nbPreviewInfo.Text = string.Format(
                    "Population: {0} people. Matched: {1}. Showing sample of {2}.",
                    populationIds.Count, matchedResults.Count, previewData.Count );
                nbPreviewInfo.NotificationBoxType = NotificationBoxType.Info;

                gPreview.DataSource = previewData.OrderBy( r => r.Action ).ThenBy( r => r.PersonName ).ToList();
                gPreview.DataBind();
            }
        }

        private void ExecuteCalculation()
        {
            // This leverages the same job logic but for a single calculation
            nbPreviewInfo.Text = "Execution from UI is a future enhancement. For now, use the 'Run Now' button on the Service Job.";
            nbPreviewInfo.NotificationBoxType = NotificationBoxType.Info;
        }

        #endregion

        #region Helper Classes

        private class PreviewRow
        {
            public string PersonName { get; set; }
            public string CurrentValue { get; set; }
            public string NewValue { get; set; }
            public string Action { get; set; }
        }

        #endregion
    }
}
