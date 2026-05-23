<%@ Control Language="C#" AutoEventWireup="true" CodeFile="FilterConditionsEditor.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.FilterConditionsEditor" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-filter-conditions-editor">
    <div class="row margin-b-sm">
        <div class="col-md-6">
            <Rock:RockCheckBox ID="cbMatchAll" runat="server" Label="Match All"
                Help="When enabled, ALL conditions must be met (AND). When disabled, ANY condition can be met (OR)." />
        </div>
        <div class="col-md-6 text-right">
            <asp:LinkButton ID="lbToggleRaw" runat="server" CssClass="btn btn-link btn-xs"
                OnClick="lbToggleRaw_Click" CausesValidation="false">
                <i class="fa fa-code"></i>
                <asp:Literal ID="lToggleRawText" runat="server" Text="Show raw JSON" />
            </asp:LinkButton>
        </div>
    </div>

    <asp:Panel ID="pnlRows" runat="server">
        <asp:PlaceHolder ID="phNoRows" runat="server" Visible="false">
            <p class="text-muted"><em>No filter conditions configured. Click <strong>Add Condition</strong> to begin &mdash; or, while no conditions are set, this calculation matches nobody.</em></p>
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
    </asp:Panel>

    <div class="margin-t-sm">
        <asp:LinkButton ID="lbAddRow" runat="server" CssClass="btn btn-default btn-xs"
            OnClick="lbAddRow_Click" CausesValidation="false">
            <i class="fa fa-plus"></i> Add Condition
        </asp:LinkButton>
    </div>

    <asp:Panel ID="pnlRaw" runat="server" Visible="false" CssClass="margin-t-md">
        <Rock:CodeEditor ID="ceRawJson" runat="server" Label="Raw JSON (advanced)"
            EditorMode="JavaScript" EditorTheme="Rock" EditorHeight="180"
            Help="Direct edit of the FilterConditions JSON. Edits made here are NOT applied unless you click 'Apply Raw JSON' below." />
        <asp:LinkButton ID="lbApplyRaw" runat="server" CssClass="btn btn-warning btn-xs"
            OnClick="lbApplyRaw_Click" CausesValidation="false">
            <i class="fa fa-check"></i> Apply Raw JSON
        </asp:LinkButton>
        <Rock:NotificationBox ID="nbRawError" runat="server" NotificationBoxType="Danger" Visible="false" />
    </asp:Panel>

    <asp:HiddenField ID="hfShowRaw" runat="server" Value="false" />
</asp:Panel>
