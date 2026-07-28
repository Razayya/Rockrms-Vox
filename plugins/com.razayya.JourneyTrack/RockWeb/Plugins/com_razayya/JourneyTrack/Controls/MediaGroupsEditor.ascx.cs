using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;

using com.razayya.JourneyTrack.CalculationTypes;
using com.razayya.JourneyTrack.Model;

using Rock.Data;

namespace RockWeb.Plugins.com_razayya.JourneyTrack.Controls
{
    /// <summary>
    /// Drag-and-drop editor for a Stage's <c>MediaGroupsJson</c> — organizes the Stage's
    /// active MediaWatched calculations ("media items") into named ordered sequences
    /// (groups). Groups on the left, an item drawer on the right; drag items between them
    /// and reorder within a group. State lives in a single hidden field as JSON; the server
    /// renders the lists from it on each load and reads it back on Save, so the editor
    /// survives async postbacks with no Repeater/ViewState churn.
    ///
    /// Mirrors the <c>StageLogicEditor</c> public surface: set <see cref="StageId"/> BEFORE
    /// <see cref="Value"/>.
    /// </summary>
    public partial class MediaGroupsEditor : UserControl
    {
        #region Public API

        /// <summary>The Stage whose active MediaWatched calcs populate the editor. Set BEFORE Value.</summary>
        public int StageId
        {
            get { return ( ViewState["StageId"] as int? ) ?? 0; }
            set { ViewState["StageId"] = value; }
        }

        /// <summary>Get/set the Stage's MediaGroupsJson. Canonicalized (empty groups dropped,
        /// calc Ids de-duplicated across groups) on the way in and out.</summary>
        public string Value
        {
            get { return StageMediaGroups.ToJson( StageMediaGroups.Parse( hfLayout.Value ) ); }
            set { hfLayout.Value = StageMediaGroups.ToJson( StageMediaGroups.Parse( value ) ); }
        }

        /// <summary>Currently always empty — the drag model can't create duplicates and Parse
        /// de-dupes; kept for parity with the other editors so the host can call it uniformly.</summary>
        public List<string> GetValidationErrors()
        {
            return new List<string>();
        }

        #endregion

        #region Calc loading

        // Per-postback cache of this Stage's active MediaWatched calcs.
        private List<JourneyCalculation> _calcs;

        private List<JourneyCalculation> GetMediaCalcs()
        {
            if ( _calcs != null ) return _calcs;
            if ( StageId <= 0 ) return _calcs = new List<JourneyCalculation>();

            var mediaTypeName = typeof( MediaWatchedCalculation ).FullName;
            using ( var rockContext = new RockContext() )
            {
                _calcs = new JourneyCalculationService( rockContext ).Queryable()
                    .Include( c => c.CalculationTypeEntityType )
                    .Where( c => c.StageId == StageId
                        && c.IsActive
                        && c.CalculationTypeEntityType.Name == mediaTypeName )
                    .OrderBy( c => c.Order )
                    .ThenBy( c => c.Name )
                    .ToList();
            }
            return _calcs;
        }

        /// <summary>The Stage's Guid (for the copy-shortcode buttons). Empty when unset.</summary>
        private Guid GetStageGuid()
        {
            if ( StageId <= 0 ) return Guid.Empty;
            using ( var rockContext = new RockContext() )
            {
                return new StageService( rockContext ).Queryable().AsNoTracking()
                    .Where( s => s.Id == StageId )
                    .Select( s => s.Guid )
                    .FirstOrDefault();
            }
        }

        #endregion

        protected override void OnPreRender( EventArgs e )
        {
            base.OnPreRender( e );
            RenderEditor();
            RegisterScript();
        }

        #region Rendering

        private void RenderEditor()
        {
            // Stamp the editor with its Stage Guid so the copy-shortcode buttons can build a
            // ready-to-paste {[ stagevideos ]} for this Stage (read client-side from data-stage-guid).
            pnlEditor.Attributes["data-stage-guid"] = GetStageGuid().ToString();

            var calcs = GetMediaCalcs();
            var calcById = calcs.ToDictionary( c => c.Id );
            var config = StageMediaGroups.Parse( hfLayout.Value );

            // Group panels — only render calcIds that still exist as active media calcs, and
            // keep a calc in the first group that claims it (defensive; Parse already de-dupes).
            var grouped = new HashSet<int>();
            var sbGroups = new StringBuilder();
            foreach ( var group in config.Groups )
            {
                var validIds = group.CalcIds.Where( id => calcById.ContainsKey( id ) && grouped.Add( id ) ).ToList();
                sbGroups.Append( RenderGroupPanel( group.Key, group.Name, validIds, calcById ) );
            }
            lGroups.Text = sbGroups.ToString();

            // Drawer — every active media calc not in a group, in calc order.
            var sbPalette = new StringBuilder();
            foreach ( var calc in calcs )
            {
                if ( grouped.Contains( calc.Id ) ) continue;
                sbPalette.Append( RenderItem( calc.Id, calc.Name ) );
            }
            lPalette.Text = sbPalette.ToString();

            nbNoCalcs.Visible = calcs.Count == 0;
            pnlBody.Visible = calcs.Count > 0;
        }

        private static string RenderItem( int calcId, string name )
        {
            return string.Format(
                "<li class=\"jt-mg-item\" data-calc-id=\"{0}\">" +
                  "<i class=\"fa fa-bars jt-mg-item-handle\"></i>" +
                  "<i class=\"fa fa-video jt-mg-item-icon\"></i>" +
                  "<span class=\"jt-mg-item-name\">{1}</span>" +
                "</li>",
                calcId,
                HttpUtility.HtmlEncode( name ) );
        }

        private string RenderGroupPanel( string key, string name, List<int> calcIds, Dictionary<int, JourneyCalculation> calcById )
        {
            var sbItems = new StringBuilder();
            foreach ( var id in calcIds )
            {
                if ( calcById.TryGetValue( id, out var calc ) )
                {
                    sbItems.Append( RenderItem( calc.Id, calc.Name ) );
                }
            }

            return string.Format(
                "<div class=\"panel panel-widget js-mg-group jt-mg-group\" data-key=\"{0}\">" +
                  "<div class=\"panel-heading jt-mg-group-head\">" +
                    "<i class=\"fa fa-bars jt-mg-group-handle\" title=\"Reorder group\"></i>" +
                    "<input type=\"text\" class=\"js-mg-group-name jt-mg-group-name\" placeholder=\"Group name\" value=\"{1}\" />" +
                    "<button type=\"button\" class=\"btn btn-default btn-xs js-mg-copy-shortcode\" title=\"Copy the stagevideos shortcode for this group\"><i class=\"fa fa-clipboard\"></i></button>" +
                    "<button type=\"button\" class=\"btn btn-danger btn-xs js-mg-remove-group\" title=\"Remove group\"><i class=\"fa fa-times\"></i></button>" +
                  "</div>" +
                  "<div class=\"panel-body\"><ul class=\"jt-mg-list\">{2}</ul></div>" +
                "</div>",
                HttpUtility.HtmlAttributeEncode( key ?? string.Empty ),
                HttpUtility.HtmlAttributeEncode( name ?? string.Empty ),
                sbItems.ToString() );
        }

        #endregion

        #region Client script

        private void RegisterScript()
        {
            // All single-quoted JS so this stays a zero-escape C# verbatim string. The IIFE is
            // re-emitted on every (partial) postback: it redefines its window.* helpers (cheap)
            // and re-binds sortable, but guards the one-time document delegation + Sys add_load
            // hook behind __jtMgBound so they don't accumulate.
            const string script = @"
(function () {
    window.jtMgSerialize = function ($ed) {
        var groups = [];
        $ed.find('.jt-mg-groups-list > .js-mg-group').each(function () {
            var $g = $(this);
            var calcIds = [];
            $g.find('.jt-mg-list > li[data-calc-id]').each(function () {
                var id = parseInt($(this).attr('data-calc-id'), 10);
                if (id) { calcIds.push(id); }
            });
            groups.push({ key: $g.attr('data-key') || '', name: $g.find('.js-mg-group-name').val() || '', calcIds: calcIds });
        });
        var hf = $ed.find('.js-mg-hf')[0];
        if (hf) { hf.value = JSON.stringify({ groups: groups }); }
    };

    window.jtMgInit = function () {
        var $editors = $('.jt-mg-editor');
        if (!$editors.length) { return; }
        $editors.each(function () {
            var $ed = $(this);
            $ed.find('.jt-mg-list').each(function () {
                var $list = $(this);
                if ($list.hasClass('ui-sortable')) { try { $list.sortable('destroy'); } catch (e) {} }
                $list.sortable({
                    connectWith: '.jt-mg-list',
                    handle: '.jt-mg-item-handle',
                    placeholder: 'jt-mg-placeholder',
                    forcePlaceholderSize: true,
                    tolerance: 'pointer',
                    cursor: 'move',
                    update: function () { window.jtMgSerialize($ed); }
                }).disableSelection();
            });
            var $glist = $ed.find('.jt-mg-groups-list');
            if ($glist.length) {
                if ($glist.hasClass('ui-sortable')) { try { $glist.sortable('destroy'); } catch (e) {} }
                $glist.sortable({
                    items: '> .js-mg-group',
                    handle: '.jt-mg-group-handle',
                    placeholder: 'jt-mg-placeholder',
                    tolerance: 'pointer',
                    update: function () { window.jtMgSerialize($ed); }
                }).disableSelection();
            }
        });
    };

    window.jtMgAddGroup = function ($ed) {
        var key = 'g' + (new Date().getTime());
        var $panel = $('<div>').addClass('panel panel-widget js-mg-group jt-mg-group').attr('data-key', key);
        var $head = $('<div>').addClass('panel-heading jt-mg-group-head');
        $head.append($('<i>').addClass('fa fa-bars jt-mg-group-handle').attr('title', 'Reorder group'));
        $head.append($('<input>').attr('type', 'text').addClass('js-mg-group-name jt-mg-group-name').attr('placeholder', 'Group name'));
        $head.append($('<button>').attr('type', 'button').addClass('btn btn-default btn-xs js-mg-copy-shortcode').attr('title', 'Copy the stagevideos shortcode for this group').append($('<i>').addClass('fa fa-clipboard')));
        $head.append($('<button>').attr('type', 'button').addClass('btn btn-danger btn-xs js-mg-remove-group').attr('title', 'Remove group').append($('<i>').addClass('fa fa-times')));
        var $body = $('<div>').addClass('panel-body').append($('<ul>').addClass('jt-mg-list'));
        $panel.append($head).append($body);
        $ed.find('.jt-mg-groups-list').append($panel);
        window.jtMgInit();
        window.jtMgSerialize($ed);
    };

    if (!window.__jtMgBound) {
        window.__jtMgBound = true;
        $(document)
            .on('click.jtmg', '.js-mg-add-group', function (e) {
                e.preventDefault();
                window.jtMgAddGroup($(this).closest('.jt-mg-editor'));
            })
            .on('click.jtmg', '.js-mg-remove-group', function (e) {
                e.preventDefault();
                var $ed = $(this).closest('.jt-mg-editor');
                var $g = $(this).closest('.js-mg-group');
                $g.find('.jt-mg-list > li[data-calc-id]').appendTo($ed.find('.js-mg-palette'));
                $g.remove();
                window.jtMgSerialize($ed);
            })
            .on('keyup.jtmg change.jtmg', '.js-mg-group-name', function () {
                window.jtMgSerialize($(this).closest('.jt-mg-editor'));
            })
            .on('click.jtmg', '.js-mg-copy-shortcode', function (e) {
                e.preventDefault();
                var $btn = $(this);
                var $ed = $btn.closest('.jt-mg-editor');
                var q = String.fromCharCode(39);
                var stageGuid = $ed.attr('data-stage-guid') || '';
                var grp = $btn.attr('data-group');
                if (!grp) {
                    var $g = $btn.closest('.js-mg-group');
                    grp = ($g.find('.js-mg-group-name').val() || $g.attr('data-key') || '').replace(/^\s+|\s+$/g, '');
                }
                var sc = '{[ stagevideos stage:' + q + stageGuid + q
                    + ' personid:' + q + '{{ CurrentPerson.Id }}' + q
                    + ' personaliasguid:' + q + q
                    + ' group:' + q + grp + q
                    + ' showcompleted:' + q + 'false' + q + ' ]}';
                var done = function () {
                    var html = $btn.html();
                    $btn.html('<i class=' + q + 'fa fa-check' + q + '></i> Copied');
                    setTimeout(function () { $btn.html(html); }, 1500);
                };
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(sc).then(done, done);
                } else {
                    var $t = $('<textarea>').css({ position: 'absolute', left: '-9999px' }).val(sc).appendTo('body');
                    $t[0].select();
                    try { document.execCommand('copy'); } catch (err) {}
                    $t.remove();
                    done();
                }
            });
        if (window.Sys && Sys.Application) {
            Sys.Application.add_load(function () { window.jtMgInit(); });
        }
    }

    window.jtMgInit();
})();
";
            ScriptManager.RegisterStartupScript( this, GetType(), "jt-media-groups-editor", script, true );
        }

        #endregion
    }
}
