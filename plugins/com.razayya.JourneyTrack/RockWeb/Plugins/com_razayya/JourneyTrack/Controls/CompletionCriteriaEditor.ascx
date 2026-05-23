<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CompletionCriteriaEditor.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.CompletionCriteriaEditor" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-completion-criteria-editor">
    <Rock:NotificationBox ID="nbNoSiblings" runat="server" NotificationBoxType="Info" Visible="false"
        Text="No sibling Calculations are available in this Stage yet. Add other calculations to the same Stage to reference them here." />

    <asp:Panel ID="pnlRows" runat="server">
        <asp:PlaceHolder ID="phNoRows" runat="server" Visible="false">
            <p class="text-muted"><em>No completion criteria configured. Click <strong>Add Criterion</strong> to begin &mdash; without criteria, this calculation matches nobody.</em></p>
        </asp:PlaceHolder>

        <asp:Repeater ID="rRows" runat="server"
            OnItemDataBound="rRows_ItemDataBound"
            OnItemCommand="rRows_ItemCommand">
            <ItemTemplate>
                <div class="panel panel-widget margin-b-sm">
                    <div class="panel-body padding-all-sm">
                        <div class="row">
                            <div class="col-md-4">
                                <Rock:RockDropDownList ID="ddlCalc" runat="server" Label="Calculation"
                                    EnhanceForLongLists="true" Required="true" />
                            </div>
                            <div class="col-md-2">
                                <Rock:RockCheckBox ID="cbRequired" runat="server" Label="Required" Checked="true"
                                    Help="When unchecked, this criterion is optional (informational only)." />
                            </div>
                            <div class="col-md-3">
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
    </asp:Panel>

    <div class="margin-t-sm">
        <asp:LinkButton ID="lbAddRow" runat="server" CssClass="btn btn-default btn-xs"
            OnClick="lbAddRow_Click" CausesValidation="false">
            <i class="fa fa-plus"></i> Add Criterion
        </asp:LinkButton>
    </div>
</asp:Panel>
