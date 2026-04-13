using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes;
using com.razayya.CustomPersonAttributeSyncEngine.Data;
using com.razayya.CustomPersonAttributeSyncEngine.Model;
using System.Web.UI.WebControls;

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
                PopulateValidCategoriesPanel();
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
            nbWarning.Visible = false;

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

                var selectedAttributeId = apTargetAttribute.SelectedValueAsInt();
                if ( !selectedAttributeId.HasValue || selectedAttributeId.Value == 0 )
                {
                    nbWarning.Text = "A Target Person Attribute is required.";
                    nbWarning.Visible = true;
                    return;
                }

                calc.PersonAttributeId = selectedAttributeId.Value;
                calc.ResultLavaTemplate = ceResultLava.Text;
                calc.NoMatchBehavior = ddlNoMatchBehavior.SelectedValue.AsInteger() == 1
                    ? NoMatchBehavior.WriteLava
                    : NoMatchBehavior.LeaveUnchanged;
                calc.NoMatchLavaTemplate = ceNoMatchLava.Text;

                if ( calc.NoMatchBehavior == NoMatchBehavior.WriteLava && string.IsNullOrWhiteSpace( calc.NoMatchLavaTemplate ) )
                {
                    nbWarning.Text = "A No Match Lava Template is required when No Match Behavior is set to 'Write Lava Value'.";
                    nbWarning.Visible = true;
                    return;
                }

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

                if ( calc.CalculationTypeEntityTypeId == 0 )
                {
                    nbWarning.Text = "A Calculation Type is required.";
                    nbWarning.Visible = true;
                    return;
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

        protected void btnCopy_Click( object sender, EventArgs e )
        {
            var service = new ImportExportService();
            int newId = service.CopyCalculation( CalculationId );
            if ( newId > 0 )
            {
                NavigateToCurrentPageReference( new Dictionary<string, string> { { "CalculationId", newId.ToString() } } );
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
            RunPreview( null );
        }

        protected void btnPreviewSinglePerson_Click( object sender, EventArgs e )
        {
            var personId = ppSinglePerson.PersonId;
            if ( personId.HasValue )
            {
                RunPreview( new HashSet<int> { personId.Value } );
            }
            else
            {
                nbPreviewInfo.Text = "Please select a person first.";
                nbPreviewInfo.NotificationBoxType = NotificationBoxType.Warning;
            }
        }

        protected void btnExecuteSinglePerson_Click( object sender, EventArgs e )
        {
            var personId = ppSinglePerson.PersonId;
            if ( !personId.HasValue )
            {
                nbPreviewInfo.Text = "Please select a person first.";
                nbPreviewInfo.NotificationBoxType = NotificationBoxType.Warning;
                return;
            }

            ExecuteForPopulation( new HashSet<int> { personId.Value } );
        }

        protected void btnExecutePreview_Click( object sender, EventArgs e )
        {
            ExecuteForPopulation( null );
        }

        protected void btnClosePreview_Click( object sender, EventArgs e )
        {
            pnlPreview.Visible = false;
            pnlView.Visible = true;
            nbExecutionResult.Visible = false;
        }

        #endregion

        #region Methods

        private void PopulateDropDowns()
        {
            ddlNoMatchBehavior.Items.Clear();
            ddlNoMatchBehavior.Items.Add( new System.Web.UI.WebControls.ListItem( "Leave Unchanged", "0" ) );
            ddlNoMatchBehavior.Items.Add( new System.Web.UI.WebControls.ListItem( "Write Lava Value", "1" ) );

            // Load calculation types
            cpCalculationType.Items.Clear();
            cpCalculationType.Items.Add( new ListItem() );
            foreach ( var calcType in CalculationTypeComponent.GetAllTypes() )
            {
                cpCalculationType.Items.Add( new ListItem( calcType.Title, calcType.EntityTypeGuid.ToString().ToUpper() ) );
            }

            // Resolve allowed categories from the parent CalculationGroup
            var allowedCategoryIds = GetParentGroupCategoryIds();

            // Load Person attributes for the target attribute picker
            apTargetAttribute.Items.Clear();
            apTargetAttribute.Items.Add( new System.Web.UI.WebControls.ListItem() );

            var personEntityTypeId = EntityTypeCache.Get( typeof( Person ) ).Id;
            var personAttributes = AttributeCache.All()
                .Where( a => a.EntityTypeId == personEntityTypeId && a.IsActive )
                .Where( a => !allowedCategoryIds.Any() || a.Categories.Any( c => allowedCategoryIds.Contains( c.Id ) ) )
                .OrderBy( a => a.Categories.FirstOrDefault()?.Name )
                .ThenBy( a => a.Name )
                .ToList();

            foreach ( var attr in personAttributes )
            {
                var categoryPrefix = attr.Categories.Any()
                    ? attr.Categories.First().Name + " - "
                    : string.Empty;
                apTargetAttribute.Items.Add( new System.Web.UI.WebControls.ListItem(
                    categoryPrefix + attr.Name, attr.Id.ToString() ) );
            }
        }

        /// <summary>
        /// Gets the allowed Person Attribute Category IDs from the parent CalculationGroup.
        /// </summary>
        private List<int> GetParentGroupCategoryIds()
        {
            int subGroupId = ParentSubGroupId;
            if ( subGroupId == 0 && CalculationId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    subGroupId = new CalculationService( rockContext )
                        .GetSelect( CalculationId, c => c.CalculationSubGroupId );
                }
            }

            if ( subGroupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    var group = new CalculationSubGroupService( rockContext ).Queryable()
                        .Where( sg => sg.Id == subGroupId )
                        .Select( sg => sg.CalculationGroup )
                        .FirstOrDefault();

                    if ( group != null )
                    {
                        group.LoadAttributes( rockContext );
                        var categoryGuids = group.GetAttributeValue( "PersonAttributeCategories" );
                        if ( !string.IsNullOrWhiteSpace( categoryGuids ) )
                        {
                            return categoryGuids.SplitDelimitedValues()
                                .Select( g => CategoryCache.Get( g.AsGuid() ) )
                                .Where( c => c != null )
                                .Select( c => c.Id )
                                .ToList();
                        }
                    }
                }
            }

            return new List<int>();
        }

        private void PopulateValidCategoriesPanel()
        {
            var categoryIds = GetParentGroupCategoryIds();
            if ( !categoryIds.Any() )
            {
                pnlValidCategories.Visible = false;
                return;
            }

            pnlValidCategories.Visible = true;
            var categoryNames = categoryIds
                .Select( id => CategoryCache.Get( id ) )
                .Where( c => c != null )
                .OrderBy( c => c.Name )
                .Select( c => string.Format( "<li>{0}</li>", c.Name ) );

            lValidCategories.Text = string.Format(
                "<p class='text-muted'>Target attributes are restricted to the following categories (configured on the parent Calculation Group):</p><ul>{0}</ul>",
                string.Join( "", categoryNames ) );
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

            var component = CalculationTypeComponent.GetComponent( entityType.Name );
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

        private void RunPreview( HashSet<int> personIdOverride )
        {
            pnlView.Visible = false;
            pnlPreview.Visible = true;
            nbExecutionResult.Visible = false;

            var service = new SyncEngineService();
            var preview = service.PreviewCalculation( CalculationId, personIdOverride );

            if ( !string.IsNullOrEmpty( preview.ErrorMessage ) )
            {
                nbPreviewInfo.Text = preview.ErrorMessage;
                nbPreviewInfo.NotificationBoxType = NotificationBoxType.Warning;
                return;
            }

            nbPreviewInfo.Text = string.Format(
                "Population: {0} people. Matched: {1}. Showing {2} rows.",
                preview.TotalPopulation, preview.MatchedCount, preview.Rows.Count );
            nbPreviewInfo.NotificationBoxType = NotificationBoxType.Info;

            gPreview.DataSource = preview.Rows;
            gPreview.DataBind();
        }

        private const int MaxOnDemandPopulation = 50;

        private void ExecuteForPopulation( HashSet<int> personIdOverride )
        {
            pnlView.Visible = false;
            pnlPreview.Visible = true;

            // Guard against large populations — full runs should use the nightly job
            if ( personIdOverride == null || personIdOverride.Count == 0 )
            {
                var service = new SyncEngineService();
                var preview = service.PreviewCalculation( CalculationId, null, 0 );
                if ( preview.TotalPopulation > MaxOnDemandPopulation )
                {
                    nbExecutionResult.Visible = true;
                    nbExecutionResult.NotificationBoxType = NotificationBoxType.Warning;
                    nbExecutionResult.Text = string.Format(
                        "Population of {0} exceeds the on-demand limit of {1}. Use the \"Execute for Person\" button to test individuals, or run the <strong>Custom Person Attribute Sync Engine</strong> job from Admin Tools &gt; System Settings &gt; Jobs Administration.",
                        preview.TotalPopulation, MaxOnDemandPopulation );
                    return;
                }
            }

            var execService = new SyncEngineService { RunByPersonAliasId = CurrentPersonAliasId };
            SyncResult result;

            if ( personIdOverride != null && personIdOverride.Count > 0 )
            {
                result = execService.ProcessCalculation( CalculationId, personIdOverride );
            }
            else
            {
                result = execService.ProcessCalculation( CalculationId );
            }

            nbExecutionResult.Visible = true;

            if ( result.Errors.Any() )
            {
                nbExecutionResult.NotificationBoxType = NotificationBoxType.Warning;
                nbExecutionResult.Text = string.Format(
                    "Execution completed with errors. {0} updated, {1} skipped, {2} error(s).<br/>{3}",
                    result.Updated, result.Skipped, result.Errors.Count,
                    string.Join( "<br/>", result.Errors ) );
            }
            else
            {
                nbExecutionResult.NotificationBoxType = NotificationBoxType.Success;
                nbExecutionResult.Text = string.Format(
                    "Execution completed successfully. {0} attribute(s) updated, {1} skipped.",
                    result.Updated, result.Skipped );
            }

            // Refresh preview to show new current values
            RunPreview( personIdOverride );
        }

        #endregion
    }
}
