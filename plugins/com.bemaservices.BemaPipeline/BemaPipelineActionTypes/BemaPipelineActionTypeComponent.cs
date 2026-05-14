
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using com.bemaservices.BemaPipeline.Attribute;
using com.bemaservices.BemaPipeline.Model;
using com.bemaservices.BemaPipeline.Rest.API;
using com.bemaservices.BemaPipeline.Web.Cache;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Extension;
using Rock.Field.Types;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.BemaPipeline.BemaPipelineActionTypes
{
    #region Logic Settings
    [CodeEditorField( "Process Logic",
        Description = "Lava that determines whether and how the action is processed. Returns one of the following values: WaitingOnItems, ReadyForManualAction, ReadyToProcess, Completed, TimedOut, Skipped, or ActionTypeArchived. The default is WaitingOnItems.",
        IsRequired = false,
        DefaultValue = DefaultProcessLogicLava,
        Order = 0,
        Category = BaseAttributeCategories.LogicSettings,
        Key = BaseAttributeKeys.ProcessLogic,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.Lava,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 200 )]

    [WorkflowTypeField( "Timeout Workflows",
        Description = "What workflows should be launched if this action times out?",
        IsRequired = false,
        AllowMultiple = true,
        Order = 1,
        Category = BaseAttributeCategories.LogicSettings,
        Key = BaseAttributeKeys.TimeoutWorkflows )]
    #endregion Logic Settings

    #region Display Settings
    [CodeEditorField( "Display Logic",
        Description = "Lava that determines whether the action is displayed by shortcodes. Returns either True or False. The Default is True.",
        IsRequired = false,
        DefaultValue = "",
        Order = 2,
        Category = BaseAttributeCategories.LogicSettings,
        Key = BaseAttributeKeys.DisplayLogic,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.Lava,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 200 )]

    [BooleanField( "Allow Manual Override",
        Description = "Are users allowed to bypass this step and mark it complete without processing it?",
        IsRequired = true,
        DefaultBooleanValue = false,
        Order = 3,
        Category = BaseAttributeCategories.LogicSettings,
        Key = BaseAttributeKeys.AllowManualOverride )]

    [TextField( "Button Text",
        Description = "The text used for the button that opens the Pipeline Entry Block.",
        IsRequired = true,
        DefaultValue = "Launch",
        Order = 4,
        Category = BaseAttributeCategories.VisualSettings,
        Key = BaseAttributeKeys.ButtonText )]
    [TextField( "Icon Css Class",
        Description = "The icon used to represent this action",
        IsRequired = true,
        DefaultValue = "fa fa-gear",
        Order = 5,
        Category = BaseAttributeCategories.VisualSettings,
        Key = BaseAttributeKeys.IconCssClass )]
    [CodeEditorField( "Instructions",
        Description = "",
        IsRequired = false,
        DefaultValue = "",
        Order = 6,
        Category = BaseAttributeCategories.VisualSettings,
        Key = BaseAttributeKeys.Instructions,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.Lava,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 200 )]
    [CodeEditorField( "Display Lava",
        Description = "",
        IsRequired = false,
        DefaultValue = DefaultDisplayLava,
        Order = 7,
        Category = BaseAttributeCategories.VisualSettings,
        Key = BaseAttributeKeys.DisplayedLava,
        EditorMode = Rock.Web.UI.Controls.CodeEditorMode.Lava,
        EditorTheme = Rock.Web.UI.Controls.CodeEditorTheme.Rock,
        EditorHeight = 200 )]
    #endregion Display Settings

    public abstract class BemaPipelineActionTypeComponent : Component, IHasAttributes
    {
        /// <summary>
        /// Categories for the attributes
        /// </summary>
        public class BaseAttributeCategories
        {
            public const string LogicSettings = "Logic Settings";
            public const string VisualSettings = "Visual Settings";
        }

        public class BaseAttributeKeys
        {
            public const string ProcessLogic = "ProcessLogic";
            public const string TimeoutWorkflows = "TimeoutWorkflows";

            public const string DisplayLogic = "DisplayLogic";
            public const string AllowManualOverride = "AllowManualOverride";
            public const string ButtonText = "ButtonText";
            public const string IconCssClass = "IconCssClass";
            public const string Instructions = "Instructions";
            public const string DisplayedLava = "DisplayedLava";
        }

        #region Properties

        public const string DefaultDisplayLava =
                 @"
<i class='icon {{ActionType.IconCssClass}}'></i>

<div class='pipeline-content pipeline-content-{{ Action.BemaPipelineActionState | ToString | ToCssClass}}'>

    <div class='pipeline-title'>
        {{ ActionType.Name }}
    </div>

    <div class= 'pipeline-actions'>
    {% if ActionLinks == empty %}
        {% if Action.BemaPipelineActionState == 'Completed' %}
            <span class='badge badge-success'>Completed</span>
        {% else %}
            <span class='badge badge-warning'>Pending</span>
        {% endif %}
    {% endif %}
    {% for link in ActionLinks %}
        <a class='btn btn-sm {{link.ButtonUtilityClass}}'
            href='{{link.Url}}'
            data-action='{{link.ActionId}}'>
            {{ link.Label }}
        </a>
    {% endfor %}
    </div>
</div>";

        public const string DefaultProcessLogicLava =
                @"
{%- assign logic = 'ReadyToProcess' -%}
{%- for action in PreviousActions -%}
    {%- if action.CompletedDateTime == null or action.CompletedDateTime == empty -%}
        {%- assign logic = 'WaitingOnItems' -%}
        {%- break -%}
    {%- endif -%}
{%- endfor -%}
{{ logic }}";

        /// <summary>
        /// Gets the component title to be displayed to the user.
        /// </summary>
        /// <value>
        /// The component title to be displayed to the user.
        /// </value>
        public virtual string Title
        {
            get
            {
                var metadata = GetType().GetCustomAttributes( typeof( ExportMetadataAttribute ), false )
                    .Cast<ExportMetadataAttribute>()
                    .Where( m => m.Name == "ComponentName" && m.Value != null )
                    .FirstOrDefault();

                if ( metadata != null )
                {
                    return ( string ) metadata.Value;
                }
                else
                {
                    return GetType().Name;
                }
            }
        }

        public override string Description { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is active. The component is
        /// always active, it is up to the SmsAction entity to decide if the action
        /// is active or not.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        public override bool IsActive => true;

        /// <summary>
        /// Gets the order.
        /// </summary>
        /// <value>
        /// The order.
        /// </value>
        public override int Order => 0;

        public string AllowedEntityTypeNames => "";

        /// <summary>
        /// Gets the icon CSS class used to identify this component type.
        /// </summary>
        /// <value>
        /// The icon CSS class used to identify this component type.
        /// </value>
        public abstract string IconCssClass { get; }

        

        #endregion

        #region Methods


        public override string GetAttributeValue( string key )
        {
            throw new Exception( "Attributes are saved specific to the action type, which requires that the action type is included in order to load or retrieve values. Use the GetAttributeValue( BemaPipelineActionTypeCache actionType, string key ) method instead." );
        }


        public string GetAttributeValue( BemaPipelineActionTypeCache actionType, string key )
        {
            return actionType.GetAttributeValue( key );
        }


        public virtual bool ProcessState( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages, bool manualAction = false )
        {
            errorMessages = new List<string>();

            BemaPipelineActionState processState = BemaPipelineActionState.WaitingOnItems;

            string lavaTemplate = GetAttributeValue( action.ActionTypeCache, BaseAttributeKeys.ProcessLogic );
            if ( !string.IsNullOrWhiteSpace( lavaTemplate ) )
            {
                var mergeFields = GetMergeFields( action, entity );
                string parsedValue = lavaTemplate.ResolveMergeFields( mergeFields );
                processState = parsedValue.ConvertToEnum<BemaPipelineActionState>( BemaPipelineActionState.WaitingOnItems );
            }

            if ( processState == BemaPipelineActionState.ReadyForManualAction && manualAction )
            {
                //If process is in 'Allow' and a manual action is triggered, set the action to process.
                processState = BemaPipelineActionState.ReadyToProcess;
            }

            action.BemaPipelineActionState = processState;
            rockContext.SaveChanges();

            bool success = false;
            switch ( processState )
            {
                case BemaPipelineActionState.WaitingOnItems:
                    {
                        return WaitingOnItems( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.ReadyForManualAction:
                    {
                        return ReadyForManualAction( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.ReadyToProcess:
                    {
                        return ReadyToProcess( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.Completed:
                    {
                        return Completed( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.Skipped:
                    {
                        return Skipped( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.ActionTypeArchived:
                    {
                        return ActionTypeArchived( rockContext, action, entity, out errorMessages );
                    }
                case BemaPipelineActionState.TimedOut:
                    {
                        return TimedOut( rockContext, action, entity, out errorMessages );
                    }
            }

            return success;
        }

        public virtual bool WaitingOnItems( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            return false;
        }

        public virtual bool ReadyForManualAction( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            return false;
        }

        public virtual bool ReadyToProcess( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            var isActionCompleted = false;
            if ( action.ActivatedDateTime == null )
            {
                action.ActivatedDateTime = RockDateTime.Now;
            }

            isActionCompleted = ProcessAction( rockContext, action, entity, out errorMessages );
            action.LastProcessedDateTime = RockDateTime.Now;

            if ( isActionCompleted )
            {
                action.CompletedDateTime = RockDateTime.Now;
            }
            rockContext.SaveChanges();
            return isActionCompleted;
        }

        public virtual bool Skipped( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            action.LastProcessedDateTime = RockDateTime.Now;
            action.CompletedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();
            return true;
        }

        public virtual bool ActionTypeArchived( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            action.LastProcessedDateTime = RockDateTime.Now;
            action.CompletedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();
            return true;
        }

        public virtual bool TimedOut( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            LaunchTimeoutWorkflows( rockContext, action, entity, out errorMessages );
            action.LastProcessedDateTime = RockDateTime.Now;
            action.CompletedDateTime = RockDateTime.Now;
            action.BemaPipeline.MarkComplete( BemaPipelineState.Completed );
            rockContext.SaveChanges();
            return true;
        }

        public virtual bool Completed( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            action.LastProcessedDateTime = RockDateTime.Now;
            action.CompletedDateTime = RockDateTime.Now;
            rockContext.SaveChanges();
            return true;
        }


        public virtual bool ShouldDisplayAction( RockContext rockContext, BemaPipelineAction action, IEntity entity )
        {
            bool displayAction = true;

            string lavaTemplate = GetAttributeValue( action.ActionTypeCache, BaseAttributeKeys.ProcessLogic );
            if ( !string.IsNullOrWhiteSpace( lavaTemplate ) )
            {
                var mergeFields = GetMergeFields( action, entity );
                string parsedValue = lavaTemplate.ResolveMergeFields( mergeFields );
                displayAction = parsedValue.AsBoolean( true );
            }

            return displayAction;
        }

        public virtual bool LaunchTimeoutWorkflows( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            var workflowTypeGuids = GetAttributeValue( action.ActionTypeCache, BaseAttributeKeys.TimeoutWorkflows ).SplitDelimitedValues().AsGuidList();

            foreach ( var workflowTypeGuid in workflowTypeGuids )
            {
                var workflowType = WorkflowTypeCache.Get( workflowTypeGuid );
                if ( workflowType != null && ( workflowType.IsActive ?? true ) )
                {
                    var workflow = Rock.Model.Workflow.Activate( workflowType, "Timeout Workflow", rockContext );
                    new WorkflowService( rockContext ).Process( workflow, entity, out errorMessages );
                    rockContext.SaveChanges();
                }
            }

            return true;
        }

        public abstract bool ProcessAction( RockContext rockContext, BemaPipelineAction action, IEntity entity, out List<string> errorMessages );

        public abstract List<ActionLink> ActionLinks( RockContext rockContext, BemaPipelineAction action, IEntity entity, Person targetPerson, out List<string> errorMessages );


        public Dictionary<string, object> GetMergeFields( BemaPipelineAction action, IEntity entity )
        {
            var EntityType = EntityTypeCache.Get( entity.TypeId );

            var currentActionType = action.ActionTypeCache;

            var previousActions = action.BemaPipeline.BemaPipelineActions
                                        .Where( a => a.ActionTypeCache != null && a.ActionTypeCache.Order < currentActionType.Order )
                                        .OrderBy( a => a.ActionTypeCache.Order )
                                        .ToList();
            var lastAction = previousActions
                                .OrderByDescending( a => a.ActionTypeCache.Order )
                                .FirstOrDefault();

            var futureActions = action.BemaPipeline.BemaPipelineActions
                                        .Where( a => a.ActionTypeCache != null && a.ActionTypeCache.Order > currentActionType.Order )
                                        .OrderBy( a => a.ActionTypeCache.Order )
                                        .ToList();
            var nextAction = futureActions
                                .FirstOrDefault();

            var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null );
            mergeFields.Add( "Pipeline", action.BemaPipeline );
            mergeFields.Add( EntityType.FriendlyName.RemoveSpaces(), entity );
            mergeFields.Add( "EntityType", EntityType );            
            mergeFields.Add( "Action", action );
            mergeFields.Add( "ActionType", currentActionType );
            mergeFields.Add( "PreviousActions", previousActions );
            mergeFields.Add( "LastAction", lastAction );
            mergeFields.Add( "FutureActions", futureActions );
            mergeFields.Add( "NextAction", nextAction );

            return mergeFields;
        }

        #endregion
    }
}
