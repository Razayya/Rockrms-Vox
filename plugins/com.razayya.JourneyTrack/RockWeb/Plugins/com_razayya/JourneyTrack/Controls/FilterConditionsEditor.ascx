<%@ Control Language="C#" AutoEventWireup="true" CodeFile="FilterConditionsEditor.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.FilterConditionsEditor" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-filter-conditions-editor">
    <div class="row margin-b-sm">
        <div class="col-md-6">
            <Rock:RockDropDownList ID="ddlTopType" runat="server" Label="Combine groups with"
                AutoPostBack="true" OnSelectedIndexChanged="ddlTopType_SelectedIndexChanged"
                Help="How the groups below are combined. ALL = every group must match (AND). ANY = at least one group must match (OR)." />
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
                                    FormGroupCssClass="margin-b-none"
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
                            <p class="text-muted"><em>No conditions in this group.</em></p>
                        </asp:PlaceHolder>

                        <asp:Repeater ID="rRows" runat="server"
                            OnItemDataBound="rRows_ItemDataBound"
                            OnItemCommand="rRows_ItemCommand">
                            <ItemTemplate>
                                <div class="panel panel-widget margin-b-sm">
                                    <div class="panel-body padding-all-sm">
                                        <div class="row">
                                            <div class="col-md-2">
                                                <Rock:RockDropDownList ID="ddlSource" runat="server" Label="Source"
                                                    AutoPostBack="true" OnSelectedIndexChanged="ddlSource_SelectedIndexChanged" />
                                            </div>
                                            <div class="col-md-3">
                                                <Rock:RockDropDownList ID="ddlKeyProperty" runat="server" Label="Person Property"
                                                    EnhanceForLongLists="true" AutoPostBack="true" OnSelectedIndexChanged="ddlKey_SelectedIndexChanged" />
                                                <Rock:RockDropDownList ID="ddlKeyAttribute" runat="server" Label="Person Attribute"
                                                    EnhanceForLongLists="true" Visible="false"
                                                    AutoPostBack="true" OnSelectedIndexChanged="ddlKey_SelectedIndexChanged" />
                                            </div>
                                            <div class="col-md-3">
                                                <Rock:RockDropDownList ID="ddlComparison" runat="server" Label="Comparison"
                                                    AutoPostBack="true" OnSelectedIndexChanged="ddlComparison_SelectedIndexChanged" />
                                            </div>
                                            <div class="col-md-3">
                                                <Rock:RockTextBox    ID="tbValueText"     runat="server" Label="Value" Visible="false" />
                                                <Rock:RockDropDownList ID="ddlValueBool"  runat="server" Label="Value" Visible="false">
                                                    <asp:ListItem Text="True"  Value="True" />
                                                    <asp:ListItem Text="False" Value="False" />
                                                </Rock:RockDropDownList>
                                                <Rock:DatePicker     ID="dpValueDate"     runat="server" Label="Value" Visible="false" />
                                                <Rock:CampusPicker   ID="cpValueCampus"   runat="server" Label="Value" Visible="false" />
                                                <Rock:DefinedValuePicker ID="dvpValue"    runat="server" Label="Value" Visible="false" />
                                                <asp:Literal         ID="lValueHidden"    runat="server" Visible="false" />
                                            </div>
                                            <div class="col-md-1 text-right" style="padding-top: 24px;">
                                                <asp:LinkButton ID="lbDeleteRow" runat="server" CssClass="btn btn-danger btn-xs"
                                                    CommandName="DeleteRow" CommandArgument='<%# Container.ItemIndex %>'
                                                    CausesValidation="false" ToolTip="Remove this condition">
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
                                CommandName="AddCondition" CommandArgument='<%# Container.ItemIndex %>'
                                CausesValidation="false">
                                <i class="fa fa-plus"></i> Add Condition
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
            Help="Direct edit of the FilterConditions JSON. Supports the nested tree form { &quot;type&quot;: &quot;Any&quot;, &quot;children&quot;: [ ... ] } as well as a flat array. Edits here are NOT applied unless you click 'Apply Raw JSON'." />
        <asp:LinkButton ID="lbApplyRaw" runat="server" CssClass="btn btn-warning btn-xs"
            OnClick="lbApplyRaw_Click" CausesValidation="false">
            <i class="fa fa-check"></i> Apply Raw JSON
        </asp:LinkButton>
        <Rock:NotificationBox ID="nbRawError" runat="server" NotificationBoxType="Danger" Visible="false" />
    </asp:Panel>

    <asp:HiddenField ID="hfShowRaw" runat="server" Value="false" />
</asp:Panel>
