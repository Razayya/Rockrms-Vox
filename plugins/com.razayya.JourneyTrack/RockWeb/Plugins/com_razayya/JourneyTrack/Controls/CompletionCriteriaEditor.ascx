<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CompletionCriteriaEditor.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.CompletionCriteriaEditor" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-completion-criteria-editor">
    <Rock:NotificationBox ID="nbNoSiblings" runat="server" NotificationBoxType="Info" Visible="false"
        Text="No sibling Calculations are available in this Stage yet. Add other calculations to the same Stage to reference them here." />

    <div class="row margin-b-sm">
        <div class="col-md-6">
            <Rock:RockDropDownList ID="ddlTopType" runat="server" Label="Combine groups with"
                AutoPostBack="true" OnSelectedIndexChanged="ddlTopType_SelectedIndexChanged"
                Help="How the groups below are combined. All = every group must be satisfied; Any = at least one group." />
        </div>
        <div class="col-md-6 text-right">
            <asp:LinkButton ID="lbToggleRaw" runat="server" CssClass="btn btn-link btn-xs"
                OnClick="lbToggleRaw_Click" CausesValidation="false">
                <i class="fa fa-code"></i>
                <asp:Literal ID="lToggleRawText" runat="server" Text="Show raw JSON" />
            </asp:LinkButton>
        </div>
    </div>

    <Rock:NotificationBox ID="nbForceRaw" runat="server" NotificationBoxType="Info" Visible="false"
        Text="This calculation uses a logic tree deeper than the visual editor supports (groups within groups). Edit it below as raw JSON." />

    <asp:Panel ID="pnlGroups" runat="server">
        <asp:PlaceHolder ID="phNoGroups" runat="server" Visible="false">
            <p class="text-muted"><em>No groups configured. Click <strong>Add Group</strong> to begin &mdash; while empty, this calculation matches nobody.</em></p>
        </asp:PlaceHolder>

        <asp:Repeater ID="rGroups" runat="server"
            OnItemDataBound="rGroups_ItemDataBound"
            OnItemCommand="rGroups_ItemCommand">
            <ItemTemplate>
                <div class="panel panel-default margin-b-sm">
                    <div class="panel-heading">
                        <div class="row">
                            <div class="col-md-5">
                                <Rock:RockDropDownList ID="ddlGroupType" runat="server" Label="Match within group"
                                    AutoPostBack="true" OnSelectedIndexChanged="ddlGroupType_SelectedIndexChanged" />
                            </div>
                            <div class="col-md-7 text-right" style="padding-top: 24px;">
                                <asp:LinkButton ID="lbDeleteGroup" runat="server" CssClass="btn btn-danger btn-xs"
                                    CommandName="DeleteGroup" CommandArgument='<%# Container.ItemIndex %>'
                                    CausesValidation="false" ToolTip="Remove this group">
                                    <i class="fa fa-times"></i> Remove Group
                                </asp:LinkButton>
                            </div>
                        </div>
                    </div>
                    <div class="panel-body padding-all-sm">
                        <asp:PlaceHolder ID="phNoRows" runat="server" Visible="false">
                            <p class="text-muted"><em>No criteria in this group.</em></p>
                        </asp:PlaceHolder>

                        <asp:Repeater ID="rRows" runat="server"
                            OnItemDataBound="rRows_ItemDataBound"
                            OnItemCommand="rRows_ItemCommand">
                            <ItemTemplate>
                                <div class="panel panel-widget margin-b-sm">
                                    <div class="panel-body padding-all-sm">
                                        <div class="row">
                                            <div class="col-md-5">
                                                <Rock:RockDropDownList ID="ddlCalc" runat="server" Label="Calculation"
                                                    EnhanceForLongLists="true" />
                                            </div>
                                            <div class="col-md-4">
                                                <Rock:RockDropDownList ID="ddlComparison" runat="server" Label="Comparison"
                                                    AutoPostBack="true" OnSelectedIndexChanged="ddlComparison_SelectedIndexChanged" />
                                            </div>
                                            <div class="col-md-2">
                                                <Rock:RockTextBox ID="tbValue" runat="server" Label="Value" />
                                            </div>
                                            <div class="col-md-1 text-right" style="padding-top: 24px;">
                                                <asp:LinkButton ID="lbDeleteRow" runat="server" CssClass="btn btn-danger btn-xs"
                                                    CommandName="DeleteRow" CommandArgument='<%# Container.ItemIndex %>'
                                                    CausesValidation="false" ToolTip="Remove this criterion">
                                                    <i class="fa fa-times"></i>
                                                </asp:LinkButton>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>

                        <div class="margin-t-sm">
                            <asp:LinkButton ID="lbAddRow" runat="server" CssClass="btn btn-default btn-xs"
                                CommandName="AddCriterion" CommandArgument='<%# Container.ItemIndex %>'
                                CausesValidation="false">
                                <i class="fa fa-plus"></i> Add Criterion
                            </asp:LinkButton>
                        </div>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </asp:Panel>

    <div class="margin-t-sm">
        <asp:LinkButton ID="lbAddGroup" runat="server" CssClass="btn btn-default btn-xs"
            OnClick="lbAddGroup_Click" CausesValidation="false">
            <i class="fa fa-plus-square"></i> Add Group
        </asp:LinkButton>
    </div>

    <asp:Panel ID="pnlRaw" runat="server" Visible="false" CssClass="margin-t-md">
        <Rock:CodeEditor ID="ceRawJson" runat="server" Label="Raw JSON (advanced)"
            EditorMode="JavaScript" EditorTheme="Rock" EditorHeight="220"
            Help="Direct edit of the CompletionCriteria JSON. Supports the nested tree form { &quot;type&quot;: &quot;Any&quot;, &quot;children&quot;: [ ... ] } as well as a flat array. Edits here are NOT applied unless you click 'Apply Raw JSON'." />
        <asp:LinkButton ID="lbApplyRaw" runat="server" CssClass="btn btn-warning btn-xs"
            OnClick="lbApplyRaw_Click" CausesValidation="false">
            <i class="fa fa-check"></i> Apply Raw JSON
        </asp:LinkButton>
        <Rock:NotificationBox ID="nbRawError" runat="server" NotificationBoxType="Danger" Visible="false" />
    </asp:Panel>

    <asp:HiddenField ID="hfShowRaw" runat="server" Value="false" />
</asp:Panel>
