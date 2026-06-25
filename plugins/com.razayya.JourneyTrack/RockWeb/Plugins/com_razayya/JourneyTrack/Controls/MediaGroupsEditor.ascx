<%@ Control Language="C#" AutoEventWireup="true" CodeFile="MediaGroupsEditor.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.MediaGroupsEditor" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-mg-editor">

    <%-- Client-side layout state. JS keeps this in sync on every drag / add / remove / rename;
         the server reads it back on Save and re-renders the lists from it on each load. --%>
    <input type="hidden" runat="server" id="hfLayout" class="js-mg-hf" />

    <Rock:NotificationBox ID="nbNoCalcs" runat="server" NotificationBoxType="Info" Visible="false"
        Text="This Stage has no active Media Watched calculations yet. Add video calculations to the Stage first, then organize them into groups here." />

    <asp:Panel ID="pnlBody" runat="server" CssClass="jt-mg-body">
        <div class="row">
            <%-- Groups (left) --%>
            <div class="col-md-7">
                <label class="control-label">Groups</label>
                <p class="help-block">
                    Each group is its own ordered sequence &mdash; within a group a video unlocks only
                    after the previous one is watched. Groups play in parallel. Videos left under
                    <strong>Media Items</strong> form one default sequence.
                </p>
                <div class="jt-mg-groups-list">
                    <asp:Literal ID="lGroups" runat="server" />
                </div>
                <button type="button" class="btn btn-default btn-xs js-mg-add-group">
                    <i class="fa fa-plus-square"></i> Add Group
                </button>
            </div>

            <%-- Media item drawer (right) --%>
            <div class="col-md-5">
                <div class="panel panel-default jt-mg-palette-panel">
                    <div class="panel-heading">
                        <h4 class="panel-title"><i class="fa fa-film"></i> Media Items</h4>
                    </div>
                    <div class="panel-body">
                        <ul class="jt-mg-list js-mg-palette">
                            <asp:Literal ID="lPalette" runat="server" />
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    </asp:Panel>

    <style>
        .jt-mg-editor .jt-mg-list { list-style: none; margin: 0; padding: 4px; min-height: 40px; border: 1px dashed #d8d8d8; border-radius: 4px; background: #fafafa; }
        .jt-mg-editor .jt-mg-list > li { padding: 6px 8px; margin: 4px 0; background: #fff; border: 1px solid #e0e0e0; border-radius: 3px; }
        .jt-mg-editor .jt-mg-item-handle, .jt-mg-editor .jt-mg-group-handle { cursor: move; color: #b0b0b0; margin-right: 6px; }
        .jt-mg-editor .jt-mg-item-icon { color: #999; margin-right: 4px; }
        .jt-mg-editor .jt-mg-placeholder { border: 1px dashed #aac8ec; background: #eef6ff; height: 32px; margin: 4px 0; border-radius: 3px; }
        .jt-mg-editor .jt-mg-group { margin-bottom: 10px; }
        .jt-mg-editor .jt-mg-group .panel-heading { padding: 6px 10px; }
        .jt-mg-editor .jt-mg-group-head { display: flex; align-items: center; }
        .jt-mg-editor .jt-mg-group-name { flex: 1; border: 1px solid #ccc; border-radius: 3px; padding: 3px 6px; margin: 0 6px; }
        .jt-mg-editor .jt-mg-palette-panel .panel-body { padding: 8px; }
    </style>
</asp:Panel>
