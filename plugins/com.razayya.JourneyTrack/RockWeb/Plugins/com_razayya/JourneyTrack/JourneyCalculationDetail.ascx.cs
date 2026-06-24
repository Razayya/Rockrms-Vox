using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Data;
using com.razayya.JourneyTrack.Model;
using com.razayya.JourneyTrack.UI;
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

using NoMatchBehavior = com.razayya.JourneyTrack.Model.NoMatchBehavior;
// `Controls` resolves to `this.Controls` (ControlCollection) inside a RockBlock,
// shadowing the namespace. Alias keeps the editor static-method calls clean.
using JtControls = RockWeb.Plugins.com_razayya.JourneyTrack.Controls;

namespace RockWeb.Plugins.com_razayya.JourneyTrack
{
    [DisplayName( "Calculation Detail" )]
    [Category( "Razayya > JourneyTrack" )]
    [Description( "Displays details for a single Calculation with component-specific configuration." )]

    [LinkedPage( "Parent Page",
        Description = "Page to navigate back to the parent Sub Group.",
        IsRequired = false,
        Order = 0,
        Key = "ParentPage" )]

    public partial class JourneyCalculationDetail : RockBlock
    {
        #region Properties

        private int JourneyCalculationId
        {
            get { return ViewState["JourneyCalculationId"] as int? ?? 0; }
            set { ViewState["JourneyCalculationId"] = value; }
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
                int calcId = PageParameter( "JourneyCalculationId" ).AsInteger();
                int subGroupId = PageParameter( "StageId" ).AsInteger();
                ParentSubGroupId = subGroupId;

                PopulateDropDowns();
                PopulateValidCategoriesPanel();
                ShowDetail( calcId );
            }
            else if ( pnlEdit.Visible )
            {
                // Re-add dynamically-rendered component-attribute editors on every postback
                // while in edit mode. Without this, the MediaElement picker's inner AJAX
                // postback (account → folder cascade) loses its parent controls because they
                // weren't recreated this lifecycle. Any FieldType with internal postback
                // behaviour (MediaElement, Schedule, Group hierarchy, ...) had the same bug.
                var typeGuid = cpCalculationType.SelectedValue.AsGuidOrNull();
                if ( typeGuid.HasValue )
                {
                    var entityType = EntityTypeCache.Get( typeGuid.Value );
                    if ( entityType != null )
                    {
                        var calc = new JourneyCalculation
                        {
                            Id = JourneyCalculationId,
                            CalculationTypeEntityTypeId = entityType.Id,
                            StageId = ParentSubGroupId
                        };
                        LoadComponentAttributes( calc );
                    }
                }
            }
        }

        #endregion

        #region Events

        protected void btnEdit_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var calc = new JourneyCalculationService( rockContext ).Get( JourneyCalculationId );
                ShowEditDetails( calc, rockContext );
            }
        }

        protected void btnSave_Click( object sender, EventArgs e )
        {
            nbWarning.Visible = false;

            using ( var rockContext = new RockContext() )
            {
                var service = new JourneyCalculationService( rockContext );
                JourneyCalculation calc;

                if ( JourneyCalculationId != 0 )
                {
                    calc = service.Get( JourneyCalculationId );
                }
                else
                {
                    calc = new JourneyCalculation();
                    calc.StageId = ParentSubGroupId;
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
                calc.SkipIfTargetHasValue = cbSkipIfTargetHasValue.Checked;
                calc.OnMatchSystemCommunicationId = ddlOnMatchCommunication.SelectedValueAsInt();

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

                // Visual-editor validation. Runs BEFORE SaveChanges so the calc row
                // isn't persisted (new) or partially updated (existing) when its
                // configuration is incomplete.
                var componentTypeName = EntityTypeCache.Get( calc.CalculationTypeEntityTypeId )?.Name ?? string.Empty;
                List<string> editorErrors = null;
                if ( componentTypeName.EndsWith( ".PersonFilterCalculation" ) && fcEditor.Visible )
                {
                    editorErrors = fcEditor.GetValidationErrors();
                }
                else if ( componentTypeName.EndsWith( ".CompletionCalculation" ) && ccEditor.Visible )
                {
                    editorErrors = ccEditor.GetValidationErrors();
                }
                else if ( componentTypeName.EndsWith( ".GroupAttendanceCalculation" ) && gaEditor.Visible )
                {
                    editorErrors = gaEditor.GetValidationErrors();
                }
                if ( editorErrors != null && editorErrors.Count > 0 )
                {
                    nbWarning.Text = "<strong>Please fix the following before saving:</strong><ul><li>"
                        + string.Join( "</li><li>", editorErrors.Select( System.Web.HttpUtility.HtmlEncode ) )
                        + "</li></ul>";
                    nbWarning.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Warning;
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

                // The visual editors are excluded from AddEditControls (so
                // GetEditValues does not see them). Pull their values directly.
                var componentName = calc.CalculationTypeEntityType?.Name
                    ?? EntityTypeCache.Get( calc.CalculationTypeEntityTypeId )?.Name
                    ?? string.Empty;

                if ( componentName.EndsWith( ".PersonFilterCalculation" ) && fcEditor.Visible )
                {
                    calc.SetAttributeValue( "FilterConditions", fcEditor.Value );
                    calc.SetAttributeValue( "MatchAll", fcEditor.GetMatchAll() ? "True" : "False" );
                }
                else if ( componentName.EndsWith( ".CompletionCalculation" ) && ccEditor.Visible )
                {
                    calc.SetAttributeValue( "CompletionCriteria", ccEditor.Value );
                }
                else if ( componentName.EndsWith( ".GroupAttendanceCalculation" ) && gaEditor.Visible )
                {
                    calc.SetAttributeValue( "Groups_GroupAttendance", gaEditor.Value );
                }

                calc.SaveAttributeValues( rockContext );

                JourneyCalculationId = calc.Id;
                ParentSubGroupId = calc.StageId;
            }

            ShowDetail( JourneyCalculationId );
        }

        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( JourneyCalculationId == 0 )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowDetail( JourneyCalculationId );
            }
        }

        protected void btnCopy_Click( object sender, EventArgs e )
        {
            var service = new ImportExportService();
            int newId = service.CopyCalculation( JourneyCalculationId );
            if ( newId > 0 )
            {
                NavigateToCurrentPageReference( new Dictionary<string, string> { { "JourneyCalculationId", newId.ToString() } } );
            }
        }

        protected void btnDelete_Click( object sender, EventArgs e )
        {
            int parentStageId = ParentSubGroupId;

            using ( var rockContext = new RockContext() )
            {
                var service = new JourneyCalculationService( rockContext );
                var calc = service.Get( JourneyCalculationId );
                if ( calc != null )
                {
                    // Capture the parent before delete so we can navigate back to it.
                    // The DB cascades JourneyCalculationRun history (ON DELETE CASCADE);
                    // orphaned component AttributeValues are reaped by the Rock Cleanup job.
                    parentStageId = calc.StageId;
                    service.Delete( calc );
                    rockContext.SaveChanges();
                }
            }

            NavigateToLinkedPage( "ParentPage", "StageId", parentStageId );
        }

        protected void btnBack_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "ParentPage", "StageId", ParentSubGroupId );
        }

        protected void cpCalculationType_SelectedIndexChanged( object sender, EventArgs e )
        {
            // Type changed — render the new type's settings panel from a fresh, synthetic
            // calc. Without this, LoadComponentAttributes() re-loads the stored calc by Id,
            // which still has the OLD CalculationTypeEntityTypeId, so AddEditControls keeps
            // showing the previous type's settings. Visual JSON editors are reset too.
            var componentEntityTypeGuid = cpCalculationType.SelectedValue.AsGuidOrNull();
            if ( componentEntityTypeGuid.HasValue )
            {
                var entityType = EntityTypeCache.Get( componentEntityTypeGuid.Value );
                if ( entityType != null )
                {
                    var freshCalc = new JourneyCalculation
                    {
                        CalculationTypeEntityTypeId = entityType.Id,
                        StageId = ParentSubGroupId
                    };
                    LoadComponentAttributes( freshCalc );
                    return;
                }
            }
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

            // Load JourneyCalculation types
            cpCalculationType.Items.Clear();
            cpCalculationType.Items.Add( new ListItem() );
            foreach ( var calcType in JourneyCalculationTypeComponent.GetAllTypes() )
            {
                cpCalculationType.Items.Add( new ListItem( calcType.Title, calcType.EntityTypeGuid.ToString().ToUpper() ) );
            }

            // Resolve allowed categories from the parent JourneyProgram
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
        /// Gets the allowed Person Attribute Category IDs from the parent JourneyProgram.
        /// </summary>
        private List<int> GetParentGroupCategoryIds()
        {
            int subGroupId = ParentSubGroupId;
            if ( subGroupId == 0 && JourneyCalculationId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    subGroupId = new JourneyCalculationService( rockContext )
                        .GetSelect( JourneyCalculationId, c => c.StageId );
                }
            }

            if ( subGroupId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    var group = new StageService( rockContext ).Queryable()
                        .Where( sg => sg.Id == subGroupId )
                        .Select( sg => sg.JourneyProgram )
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
                "<p class='text-muted'>Target attributes are restricted to the following categories (configured on the parent Program):</p><ul>{0}</ul>",
                string.Join( "", categoryNames ) );
        }

        private void ShowDetail( int calcId )
        {
            pnlDetails.Visible = true;
            pnlPreview.Visible = false;

            JourneyCalculation calc = null;

            if ( calcId > 0 )
            {
                using ( var rockContext = new RockContext() )
                {
                    calc = new JourneyCalculationService( rockContext ).Queryable()
                        .Include( c => c.CalculationTypeEntityType )
                        .Include( c => c.PersonAttribute )
                        .Include( c => c.Stage )
                        .FirstOrDefault( c => c.Id == calcId );
                }
            }

            if ( calc == null )
            {
                calc = new JourneyCalculation { IsActive = true, StageId = ParentSubGroupId };
                lTitle.Text = ActionTitle.Add( "Calculation" ).FormatAsHtmlTitle();
                using ( var rockContext = new RockContext() )
                {
                    ShowEditDetails( calc, rockContext );
                }
                return;
            }

            JourneyCalculationId = calc.Id;
            ParentSubGroupId = calc.StageId;

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

            // Show component-specific attribute values in view mode. For known
            // JSON-input attrs (FilterConditions / CompletionCriteria), substitute
            // a friendly human-readable summary in DataView-filter style instead
            // of the raw JSON. MatchAll is folded into the summary, so we exclude
            // both keys from the generic rendering loop.
            using ( var rockContext = new RockContext() )
            {
                calc.LoadAttributes( rockContext );
                var excludeKeys = new HashSet<string>( StringComparer.OrdinalIgnoreCase ) { "Active", "Order", "MatchAll" };
                string configHtml = string.Empty;

                var componentNameForView = calc.CalculationTypeEntityType?.Name ?? string.Empty;
                if ( componentNameForView.EndsWith( ".PersonFilterCalculation" ) )
                {
                    excludeKeys.Add( "FilterConditions" );
                    var matchAll = calc.GetAttributeValue( "MatchAll" ).AsBoolean( true );
                    var summary = JtControls.FilterConditionsEditor.FormatSummaryHtml(
                        calc.GetAttributeValue( "FilterConditions" ),
                        matchAll );
                    configHtml += string.Format( "<dt>Filter Conditions</dt><dd>{0}</dd>", summary );
                }
                else if ( componentNameForView.EndsWith( ".CompletionCalculation" ) )
                {
                    excludeKeys.Add( "CompletionCriteria" );
                    var summary = JtControls.CompletionCriteriaEditor.FormatSummaryHtml(
                        calc.GetAttributeValue( "CompletionCriteria" ),
                        calc.StageId,
                        calc.Id );
                    configHtml += string.Format( "<dt>Completion Criteria</dt><dd>{0}</dd>", summary );
                }
                else if ( componentNameForView.EndsWith( ".GroupAttendanceCalculation" ) )
                {
                    excludeKeys.Add( "Groups_GroupAttendance" );
                    var summary = JtControls.GroupAttendancePicker.FormatSummaryHtml(
                        calc.GetAttributeValue( "Groups_GroupAttendance" ) );
                    configHtml += string.Format( "<dt>Groups</dt><dd>{0}</dd>", summary );
                }

                foreach ( var attr in calc.Attributes )
                {
                    if ( excludeKeys.Contains( attr.Key ) )
                    {
                        continue;
                    }

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

        private void ShowEditDetails( JourneyCalculation calc, RockContext rockContext )
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

            // Set JourneyCalculation type
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
            cbSkipIfTargetHasValue.Checked = calc.SkipIfTargetHasValue;
            JourneyTrackUiHelper.PopulateSystemCommunicationPicker( ddlOnMatchCommunication, calc.OnMatchSystemCommunicationId );

            // Load component attributes
            LoadComponentAttributes( calc, rockContext );
        }

        private void LoadComponentAttributes( JourneyCalculation calc = null, RockContext rockContext = null )
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

            var component = JourneyCalculationTypeComponent.GetComponent( entityType.Name );
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

            // Load attributes for this JourneyCalculation's component type
            bool disposeContext = rockContext == null;
            rockContext = rockContext ?? new RockContext();
            try
            {
                if ( calc == null && JourneyCalculationId > 0 )
                {
                    calc = new JourneyCalculationService( rockContext ).Get( JourneyCalculationId );
                }

                if ( calc == null )
                {
                    calc = new JourneyCalculation
                    {
                        CalculationTypeEntityTypeId = entityType.Id,
                        StageId = ParentSubGroupId
                    };
                }

                calc.LoadAttributes( rockContext );
                var excludeKeys = new List<string> { "Active", "Order" };

                // Detect known JSON-input attrs and route them to the visual editors
                // instead of the default CodeEditor textarea. The JSON attr key is excluded
                // from AddEditControls so the textarea doesn't render; the visual editor
                // becomes the source of truth on save.
                var componentName = entityType.Name ?? string.Empty;
                fcEditor.Visible = false;
                ccEditor.Visible = false;
                gaEditor.Visible = false;

                if ( componentName.EndsWith( ".PersonFilterCalculation" ) )
                {
                    excludeKeys.Add( "FilterConditions" );
                    excludeKeys.Add( "MatchAll" );
                    fcEditor.Visible = true;
                    // Only seed the editor when it doesn't already hold in-session state.
                    // This preserves the user's filter rows when they toggle calc type away
                    // and back — without this, the editor would re-seed from the (synthetic
                    // fresh) calc and wipe their rows.
                    if ( !fcEditor.HasInSessionState )
                    {
                        fcEditor.Value = calc.GetAttributeValue( "FilterConditions" );
                        fcEditor.SetMatchAll( calc.GetAttributeValue( "MatchAll" ).AsBoolean( true ) );
                    }
                }
                else if ( componentName.EndsWith( ".CompletionCalculation" ) )
                {
                    excludeKeys.Add( "CompletionCriteria" );
                    ccEditor.Visible = true;
                    ccEditor.StageId = calc.StageId;
                    ccEditor.ExcludeCalculationId = calc.Id;
                    if ( !ccEditor.HasInSessionState )
                    {
                        ccEditor.Value = calc.GetAttributeValue( "CompletionCriteria" );
                    }
                }
                else if ( componentName.EndsWith( ".GroupAttendanceCalculation" ) )
                {
                    excludeKeys.Add( "Groups_GroupAttendance" );
                    gaEditor.Visible = true;
                    if ( !gaEditor.HasInSessionState )
                    {
                        gaEditor.Value = calc.GetAttributeValue( "Groups_GroupAttendance" );
                    }
                }

                Rock.Attribute.Helper.AddEditControls( calc, phComponentAttributes, true, BlockValidationGroup, excludeKeys );
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

            var service = new JourneyTrackService();
            var preview = service.PreviewCalculation( JourneyCalculationId, personIdOverride );

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
                var service = new JourneyTrackService();
                var preview = service.PreviewCalculation( JourneyCalculationId, null, 0 );
                if ( preview.TotalPopulation > MaxOnDemandPopulation )
                {
                    nbExecutionResult.Visible = true;
                    nbExecutionResult.NotificationBoxType = NotificationBoxType.Warning;
                    nbExecutionResult.Text = string.Format(
                        "Population of {0} exceeds the on-demand limit of {1}. Use the \"Execute for Person\" button to test individuals, or run the <strong>JourneyTrack</strong> job from Admin Tools &gt; System Settings &gt; Jobs Administration.",
                        preview.TotalPopulation, MaxOnDemandPopulation );
                    return;
                }
            }

            var execService = new JourneyTrackService { RunByPersonAliasId = CurrentPersonAliasId };
            SyncResult result;

            if ( personIdOverride != null && personIdOverride.Count > 0 )
            {
                result = execService.ProcessCalculation( JourneyCalculationId, personIdOverride );
            }
            else
            {
                result = execService.ProcessCalculation( JourneyCalculationId );
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
