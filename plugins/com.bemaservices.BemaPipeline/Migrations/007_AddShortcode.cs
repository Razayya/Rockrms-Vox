using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

//using com.bemaservices.BemaPipeline.SystemGuid;
using Rock.Web.Cache;
using Rock.Lava.Blocks;
using System.Security.AccessControl;
using Rock;

namespace com.bemaservices.BemaPipeline.Migrations
{
    [MigrationNumber( 7, "1.12.5" )]
    public class AddShortcode : PipelineMigration
    {

        const string lavaShortcode = @"
{% unless pipelinetypeid > 0 %}
    <span class='alert alert-warning'>No Pipeline Type Id Set</span>
{% endunless %}

{% unless entityid > 0 %}
    <span class='alert alert-warning'>No Entity Id Set</span>
{% endunless %}

{% assign pipelineHash = pipelinetypeid | Append:'|' | Append:entityid | Md5 %}




<style>
        /*!
 * # Semantic UI 2.4.1 - Step
 * http://github.com/semantic-org/semantic-ui/
 *
 *
 * Released under the MIT license
 * http://opensource.org/licenses/MIT
 *
 */
.bema-pipeline {
    display: -webkit-inline-box;
    display: -ms-inline-flexbox;
    display: inline-flex;
    -webkit-box-orient: horizontal;
    -webkit-box-direction: normal;
    -ms-flex-direction: row;
    flex-direction: row;
    -webkit-box-align: stretch;
    -ms-flex-align: stretch;
    flex-wrap: wrap;
    align-items: stretch;
    margin: 1em 0;
    background: '';
    -webkit-box-shadow: none;
    box-shadow: none;
    line-height: 1.14285714em;
    border-radius: 0.28571429rem;
    border: 1px solid rgba(34, 36, 38, 0.15);
}
.bema-pipeline:first-child {
    margin-top: 0;
}
.bema-pipeline:last-child {
    margin-bottom: 0;
}
.bema-pipeline .pipeline-action {
    position: relative;
    display: -webkit-box;
    display: -ms-flexbox;
    display: flex;
    -webkit-box-flex: 1;
    -ms-flex: 1 0 auto;
    flex: 1 0 auto;
    -ms-flex-wrap: wrap;
    flex-wrap: wrap;
    -webkit-box-orient: horizontal;
    -webkit-box-direction: normal;
    -ms-flex-direction: row;
    flex-direction: row;
    vertical-align: middle;
    -webkit-box-align: center;
    -ms-flex-align: center;
    align-items: center;
    -webkit-box-pack: center;
    -ms-flex-pack: center;
    justify-content: center;
    margin: 0 0;
    padding: 1.14285714em 2em;
    background: #fff;
    color: rgba(0, 0, 0, 0.87);
    -webkit-box-shadow: none;
    box-shadow: none;
    border-radius: 0;
    border: none;
    border-right: 1px solid rgba(34, 36, 38, 0.15);
    -webkit-transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, -webkit-box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, -webkit-box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, box-shadow 0.1s ease, -webkit-box-shadow 0.1s ease;
}
.bema-pipeline .pipeline-action:after {
    display: none;
    position: absolute;
    z-index: 2;
    content: '';
    top: 50%;
    right: 0;
    border: medium none;
    background-color: #fff;
    width: 1.14285714em;
    height: 1.14285714em;
    border-style: solid;
    border-color: rgba(34, 36, 38, 0.15);
    border-width: 0 1px 1px 0;
    -webkit-transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, -webkit-box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, -webkit-box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, box-shadow 0.1s ease;
    transition: background-color 0.1s ease, opacity 0.1s ease, color 0.1s ease, box-shadow 0.1s ease, -webkit-box-shadow 0.1s ease;
    -webkit-transform: translateY(-50%) translateX(50%) rotate(-45deg);
    transform: translateY(-50%) translateX(50%) rotate(-45deg);
}
.bema-pipeline .pipeline-action:first-child {
    padding-left: 2em;
    border-radius: 0.28571429rem 0 0 0.28571429rem;
}
.bema-pipeline .pipeline-action:last-child {
    border-radius: 0 0.28571429rem 0.28571429rem 0;
}
.bema-pipeline .pipeline-action:last-child {
    border-right: none;
    margin-right: 0;
}
.bema-pipeline .pipeline-action:only-child {
    border-radius: 0.28571429rem;
}
.bema-pipeline .pipeline-action .pipeline-title {
    font-family: Lato, 'Helvetica Neue', Arial, Helvetica, sans-serif;
    font-size: 1.14285714em;
    font-weight: 700;
}
.bema-pipeline .pipeline-action > .pipeline-title {
    width: 100%;
}
.bema-pipeline .pipeline-action .pipeline-description {
    font-weight: 400;
    font-size: 0.92857143em;
    color: rgba(0, 0, 0, 0.87);
}
.bema-pipeline .pipeline-action > .pipeline-description {
    width: 100%;
}
.bema-pipeline .pipeline-action .pipeline-title ~ .pipeline-description {
    margin-top: 0.25em;
}
.bema-pipeline .pipeline-action > .icon {
    line-height: 1;
    font-size: 2.5em;
    margin: 0 1rem 0 0;
}
.bema-pipeline .pipeline-action > .icon,
.bema-pipeline .pipeline-action > .icon ~ .pipeline-content {
    display: block;
    -webkit-box-flex: 0;
    -ms-flex: 0 1 auto;
    flex: 0 1 auto;
    -ms-flex-item-align: middle;
    align-self: middle;
}
.bema-pipeline .pipeline-action > .icon ~ .pipeline-content {
    -webkit-box-flex: 1 0 auto;
    -ms-flex-positive: 1 0 auto;
    flex-grow: 1 0 auto;
}
.bema-pipeline:not(.vertical) .pipeline-action > .icon {
    width: auto;
}
.bema-pipeline .link.pipeline-action,
.bema-pipeline a.pipeline-action {
    cursor: pointer;
}
.bema-pipeline.ordered {
    counter-reset: ordered;
}
.bema-pipeline.ordered .pipeline-action:before {
    display: block;
    position: static;
    text-align: center;
    content: counters(ordered, '.');
    -ms-flex-item-align: middle;
    align-self: middle;
    margin-right: 1rem;
    font-size: 2.5em;
    counter-increment: ordered;
    font-family: inherit;
    font-weight: 700;
}
.bema-pipeline.ordered .pipeline-action > * {
    display: block;
    -ms-flex-item-align: middle;
    align-self: middle;
}
.bema-pipeline.vertical {
    display: -webkit-inline-box;
    display: -ms-inline-flexbox;
    display: inline-flex;
    -webkit-box-orient: vertical;
    -webkit-box-direction: normal;
    -ms-flex-direction: column;
    flex-direction: column;
    overflow: visible;
}
.bema-pipeline.vertical .pipeline-action {
    -webkit-box-pack: start;
    -ms-flex-pack: start;
    justify-content: flex-start;
    border-radius: 0;
    padding: 1.14285714em 2em;
    border-right: none;
    border-bottom: 1px solid rgba(34, 36, 38, 0.15);
}
.bema-pipeline.vertical .pipeline-action:first-child {
    padding: 1.14285714em 2em;
    border-radius: 0.28571429rem 0.28571429rem 0 0;
}
.bema-pipeline.vertical .pipeline-action:last-child {
    border-bottom: none;
    border-radius: 0 0 0.28571429rem 0.28571429rem;
}
.bema-pipeline.vertical .pipeline-action:only-child {
    border-radius: 0.28571429rem;
}
.bema-pipeline.vertical .pipeline-action:after {
    display: none;
}
.bema-pipeline.vertical .pipeline-action:after {
    top: 50%;
    right: 0;
    border-width: 0 1px 1px 0;
}
.bema-pipeline.vertical .pipeline-action:after {
    display: none;
}
.bema-pipeline.vertical .active.pipeline-action:after {
    display: block;
}
.bema-pipeline.vertical .pipeline-action:last-child:after {
    display: none;
}
.bema-pipeline.vertical .active.pipeline-action:last-child:after {
    display: block;
}
@media only screen and (max-width: 767px) {
    .bema-pipeline:not(.unstackable) {
        display: -webkit-inline-box;
        display: -ms-inline-flexbox;
        display: inline-flex;
        overflow: visible;
        -webkit-box-orient: vertical;
        -webkit-box-direction: normal;
        -ms-flex-direction: column;
        flex-direction: column;
    }
    .bema-pipeline:not(.unstackable) .pipeline-action {
        width: 100% !important;
        -webkit-box-orient: vertical;
        -webkit-box-direction: normal;
        -ms-flex-direction: column;
        flex-direction: column;
        border-radius: 0;
        padding: 1.14285714em 2em;
    }
    .bema-pipeline:not(.unstackable) .pipeline-action:first-child {
        padding: 1.14285714em 2em;
        border-radius: 0.28571429rem 0.28571429rem 0 0;
    }
    .bema-pipeline:not(.unstackable) .pipeline-action:last-child {
        border-radius: 0 0 0.28571429rem 0.28571429rem;
    }
    .bema-pipeline:not(.unstackable) .pipeline-action:after {
        display: none !important;
    }
    .bema-pipeline:not(.unstackable) .pipeline-action .pipeline-content {
        text-align: center;
    }
    .bema-pipeline.ordered:not(.unstackable) .pipeline-action:before,
    .bema-pipeline:not(.unstackable) .pipeline-action > .icon {
        margin: 0 0 1rem 0;
    }
}
.bema-pipeline .link.pipeline-action:hover,
.bema-pipeline .link.pipeline-action:hover::after,
.bema-pipeline a.pipeline-action:hover,
.bema-pipeline a.pipeline-action:hover::after {
    background: #f9fafb;
    color: rgba(0, 0, 0, 0.8);
}
.bema-pipeline .link.pipeline-action:active,
.bema-pipeline .link.pipeline-action:active::after,
.bema-pipeline a.pipeline-action:active,
.bema-pipeline a.pipeline-action:active::after {
    background: #f3f4f5;
    color: rgba(0, 0, 0, 0.9);
}
.bema-pipeline .pipeline-action.active {
    cursor: auto;
    background: #f3f4f5;
}
.bema-pipeline .pipeline-action.active:after {
    background: #f3f4f5;
}
.bema-pipeline .pipeline-action.active .pipeline-title {
    color: #4183c4;
}
.bema-pipeline.ordered .pipeline-action.active:before,
.bema-pipeline .active.pipeline-action .icon {
    color: rgba(0, 0, 0, 0.85);
}
.bema-pipeline .pipeline-action:after {
    display: block;
}
.bema-pipeline .active.pipeline-action:after {
    display: block;
}
.bema-pipeline .pipeline-action:last-child:after {
    display: none;
}
.bema-pipeline .active.pipeline-action:last-child:after {
    display: none;
}
.bema-pipeline .link.active.pipeline-action:hover,
.bema-pipeline .link.active.pipeline-action:hover::after,
.bema-pipeline a.active.pipeline-action:hover,
.bema-pipeline a.active.pipeline-action:hover::after {
    cursor: pointer;
    background: #dcddde;
    color: rgba(0, 0, 0, 0.87);
}
.bema-pipeline.ordered .pipeline-action.completed:before,
.bema-pipeline .pipeline-action.completed > .icon:before {
    color: #21ba45;
}
.bema-pipeline .disabled.pipeline-action {
    cursor: auto;
    background: #fff;
    pointer-events: none;
}
.bema-pipeline .disabled.pipeline-action,
.bema-pipeline .disabled.pipeline-action .pipeline-description,
.bema-pipeline .disabled.pipeline-action .pipeline-title {
    color: rgba(40, 40, 40, 0.3);
}
.bema-pipeline .disabled.pipeline-action:after {
    background: #fff;
}
@media only screen and (max-width: 991px) {
    .bema-pipeline[class*='tablet stackable'] {
        display: -webkit-inline-box;
        display: -ms-inline-flexbox;
        display: inline-flex;
        overflow: visible;
        -webkit-box-orient: vertical;
        -webkit-box-direction: normal;
        -ms-flex-direction: column;
        flex-direction: column;
    }
    .bema-pipeline[class*='tablet stackable'] .pipeline-action {
        -webkit-box-orient: vertical;
        -webkit-box-direction: normal;
        -ms-flex-direction: column;
        flex-direction: column;
        border-radius: 0;
        padding: 1.14285714em 2em;
        border-top: 1px solid rgba(34, 36, 38, 0.15);
    }
    .bema-pipeline[class*='tablet stackable'] .pipeline-action:first-child {
        padding: 1.14285714em 2em;
        border-radius: 0.28571429rem 0.28571429rem 0 0;
    }
    .bema-pipeline[class*='tablet stackable'] .pipeline-action:last-child {
        border-radius: 0 0 0.28571429rem 0.28571429rem;
    }
    .bema-pipeline[class*='tablet stackable'] .pipeline-action:after {
        display: none !important;
    }
    .bema-pipeline[class*='tablet stackable'] .pipeline-action .pipeline-content {
        text-align: center;
    }
    .bema-pipeline[class*='tablet stackable'].ordered .pipeline-action:before,
    .bema-pipeline[class*='tablet stackable'] .pipeline-action > .icon {
        margin: 0 0 1rem 0;
    }
}
.bema-pipeline.fluid {
    display: -webkit-box;
    display: -ms-flexbox;
    display: flex;
    width: 100%;
}
.bema-pipeline.attached {
    width: calc(100% + (--1px * 2)) !important;
    margin: 0 -1px 0;
    max-width: calc(100% + (--1px * 2));
    border-radius: 0.28571429rem 0.28571429rem 0 0;
}
.bema-pipeline.attached .pipeline-action:first-child {
    border-radius: 0.28571429rem 0 0 0;
}
.bema-pipeline.attached .pipeline-action:last-child {
    border-radius: 0 0.28571429rem 0 0;
}
.bema-pipeline.bottom.attached {
    margin: 0 -1px 0;
    border-radius: 0 0 0.28571429rem 0.28571429rem;
}
.bema-pipeline.bottom.attached .pipeline-action:first-child {
    border-radius: 0 0 0 0.28571429rem;
}
.bema-pipeline.bottom.attached .pipeline-action:last-child {
    border-radius: 0 0 0.28571429rem 0;
}
.bema-pipeline.eight,
.bema-pipeline.five,
.bema-pipeline.four,
.bema-pipeline.one,
.bema-pipeline.seven,
.bema-pipeline.six,
.bema-pipeline.three,
.bema-pipeline.two {
    width: 100%;
}
.bema-pipeline.eight > .pipeline-action,
.bema-pipeline.five > .pipeline-action,
.bema-pipeline.four > .pipeline-action,
.bema-pipeline.one > .pipeline-action,
.bema-pipeline.seven > .pipeline-action,
.bema-pipeline.six > .pipeline-action,
.bema-pipeline.three > .pipeline-action,
.bema-pipeline.two > .pipeline-action {
    -ms-flex-wrap: nowrap;
    flex-wrap: nowrap;
}
.bema-pipeline.four > .pipeline-action:nth-child(n+4),
.bema-pipeline.five > .pipeline-action:nth-child(n+5),
.bema-pipeline.six > .pipeline-action:nth-child(n+6),
.bema-pipeline.seven > .pipeline-action:nth-child(n+7),
.bema-pipeline.eight > .pipeline-action:nth-child(n+8) {
    border-top: 1px solid rgba(34, 36, 38, 0.15);
}
.bema-pipeline.one > .pipeline-action {
    width: 100%;
}
.bema-pipeline.two > .pipeline-action {
    width: 50%;
}
.bema-pipeline.three > .pipeline-action {
    width: 33.333%;
}
.bema-pipeline.four > .pipeline-action {
    width: 25%;
}
.bema-pipeline.five > .pipeline-action {
    width: 20%;
}
.bema-pipeline.six > .pipeline-action {
    width: 16.666%;
}
.bema-pipeline.seven > .pipeline-action {
    width: 14.285%;
}
.bema-pipeline.eight > .pipeline-action {
    width: 12.5%;
}
.bema-pipeline.mini.pipeline-action,
.bema-pipeline.mini .pipeline-action {
    font-size: 0.78571429rem;
}
.bema-pipeline.tiny.pipeline-action,
.bema-pipeline.tiny .pipeline-action {
    font-size: 0.85714286rem;
}
.bema-pipeline.small.pipeline-action,
.bema-pipeline.small .pipeline-action {
    font-size: 0.92857143rem;
}
.bema-pipeline.pipeline-action,
.bema-pipeline .pipeline-action {
    font-size: 1rem;
}
.bema-pipeline.large.pipeline-action,
.bema-pipeline.large .pipeline-action {
    font-size: 1.14285714rem;
}
.bema-pipeline.big.pipeline-action,
.bema-pipeline.big .pipeline-action {
    font-size: 1.28571429rem;
}
.bema-pipeline.huge.pipeline-action,
.bema-pipeline.huge .pipeline-action {
    font-size: 1.42857143rem;
}
.bema-pipeline.massive.pipeline-action,
.bema-pipeline.massive .pipeline-action {
    font-size: 1.71428571rem;
}
.bema-pipeline.ordered .pipeline-action.completed:before,
.bema-pipeline .pipeline-action.completed > .icon:before {
    content: '\e800';
}
.bema-pipeline .pipeline-actions {
    margin-top: .25em;
}

.bema-pipeline .btn {
    white-space: normal;
}

.pipeline-content {
    padding:15px;
}

.pipeline-action > .icon {
    color: #39a1b1;
}

.pipeline-actions .badge {
    min-width: fit-content;
}

</style>




<div id='{{pipelineHash}}' class='bema-pipeline four'>
    <div class='p-3 align-self-center text-center'>
        <h4><i class='fas fa-sync fa-spin'></i> <br> Loading Pipeline</h4>
    </div>
</div>





<script>

 async function renderPipeline_{{pipelineHash}}(actionId) {
    
    // replace loading div
    let pipelineDiv = document.querySelector('div[id=""{{pipelineHash}}""]');
    if( pipelineDiv )
    {
        let renderUrl = '/api/BemaPipeline/GetRenderPipelineHTML?entityId={{entityid}}&pipelineTypeId={{pipelinetypeid}}';
        if( actionId != null )
        {
            renderUrl = renderUrl.concat('&markCompleteActionId=', actionId );
        }
        const response = await fetch(renderUrl);
        const returnHtml = await response.text();
        
        // if element has no content, do not render step block
        // returnHtml..querySelectorAll('div.pipeline-action').forEach(div => { div.style.display = 'none' })
        
        pipelineDiv.innerHTML = returnHtml;
    }
    
 }
 
 //attach onclick methods to each button with data-action attribute
 document.querySelectorAll('a[data-action]').forEach( btn => 
    { 
        btn.addEventListener('click', (e) => { renderPipeline_{{pipelineHash}}( e.getAttribute('data-action') ) });
        btn.addEventListener('keyup', (e) => { if(e.Key == 'Enter' ) { renderPipeline_{{pipelineHash}}( e.getAttribute('data-action') ) } } );
    })
 
 // postback
 var prm = Sys.WebForms.PageRequestManager.getInstance();
 prm.add_endRequest(function() {
   renderPipeline_{{pipelineHash}}();
 });
 
 // initial render
 $(document).ready(function(){   
    console.log('Reached doc read');
   renderPipeline_{{pipelineHash}}();

  });
 
</script>";
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Add Shortcode
            Sql ( String.Format( @"

            INSERT INTO LavaShortcode(
                [Guid]
                , [Name]
                , [Description]
                , [Documentation]
                , [IsSystem]
                , [IsActive]
                , [TagName]
                , [Markup]
                , [TagType]
                , [EnabledLavaCommands]
                , [Parameters]
                , [CreatedDateTime])

VALUES (
'{0}'
,'BEMA Pipeline'
,'A shortcode pulling from the BEMA Pipeline to display volunteer assimilation tasks'
,'<p>{{[ bemapipeline pipelinetypeid:''0'' entityid:''0'' ]}}</p><p>{{[ endbemapipeline ]}}</p>'
,1
,1
,'bemapipeline'
,'{1}'
,2
,null
,'pipelinetypeid^|entityid^'
,GetDate()
)

            ", SystemGuid.Lava.BEMA_PIPELINE_SHORTCODE, lavaShortcode.Replace("'", "''" ) ) );


        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {

        }
    }
}
