<%@ Control Language="C#" AutoEventWireup="true" CodeFile="StageDetail.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.StageDetail" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDetails" CssClass="panel panel-block" runat="server">
            <asp:HiddenField ID="hfId" runat="server" />
            <asp:HiddenField ID="hfJourneyProgramId" runat="server" />

            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-layer-group"></i>
                    <asp:Literal ID="lTitle" runat="server" />
                </h1>
                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlInactive" runat="server" LabelType="Danger" Text="Inactive" Visible="false" />
                    <Rock:HighlightLabel ID="hlGroupName" runat="server" LabelType="Default" />
                </div>
            </div>

            <Rock:PanelDrawer ID="pdAuditDetails" runat="server" />

            <div class="panel-body">
                <Rock:NotificationBox ID="nbWarning" runat="server" NotificationBoxType="Warning" Visible="false" />

                <%-- View Mode --%>
                <asp:Panel ID="pnlView" runat="server">
                    <div class="row">
                        <div class="col-md-6">
                            <asp:Literal ID="lDescription" runat="server" />
                        </div>
                        <div class="col-md-6">
                            <dl>
                                <asp:Literal ID="lSubGroupSummary" runat="server" />
                            </dl>
                        </div>
                    </div>

                    <div class="actions">
                        <asp:LinkButton ID="btnEdit" runat="server" Text="Edit" CssClass="btn btn-primary" OnClick="btnEdit_Click" CausesValidation="false" />
                        <asp:LinkButton ID="btnCopy" runat="server" Text="Copy" CssClass="btn btn-default btn-sm" OnClick="btnCopy_Click" CausesValidation="false"
                            ToolTip="Create a copy of this sub-group including all calculations." />
                        <asp:LinkButton ID="btnBack" runat="server" Text="Back" CssClass="btn btn-link" OnClick="btnBack_Click" CausesValidation="false" />
                    </div>

                    <hr />

                    <%-- Calculations List --%>
                    <h4>Calculations</h4>
                    <div class="grid grid-panel">
                        <Rock:Grid ID="gCalculations" runat="server" RowItemText="Calculation" AllowSorting="false"
                            OnRowSelected="gCalculations_RowSelected" DisplayType="Light">
                            <Columns>
                                <Rock:ReorderField />
                                <Rock:RockBoundField DataField="Name" HeaderText="Name" />
                                <Rock:RockBoundField DataField="CalculationType" HeaderText="Type" />
                                <Rock:RockBoundField DataField="TargetAttribute" HeaderText="Target Attribute" />
                                <Rock:BoolField DataField="IsActive" HeaderText="Active" />
                                <Rock:DeleteField OnClick="gCalculations_Delete" />
                            </Columns>
                        </Rock:Grid>
                    </div>
                </asp:Panel>

                <%-- Edit Mode --%>
                <asp:Panel ID="pnlEdit" runat="server" Visible="false">
                    <asp:ValidationSummary ID="valSummary" runat="server"
                        HeaderText="Please correct the following:" CssClass="alert alert-validation" />

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:DataTextBox ID="tbName" runat="server" Label="Name" Required="true"
                                SourceTypeName="com.razayya.JourneyTrack.Model.Stage, com.razayya.JourneyTrack"
                                PropertyName="Name" />
                        </div>
                        <div class="col-md-6">
                            <Rock:RockCheckBox ID="cbIsActive" runat="server" Label="Active" />
                        </div>
                    </div>

                    <Rock:DataTextBox ID="tbDescription" runat="server" Label="Description" TextMode="MultiLine" Rows="3"
                        SourceTypeName="com.razayya.JourneyTrack.Model.Stage, com.razayya.JourneyTrack"
                        PropertyName="Description" />

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:RockCheckBoxList ID="cblPrerequisites" runat="server" Label="Prerequisite Stages"
                                Help="Select one or more sibling Stages whose Completion Calculation passers will be intersected to form this Stage's working population. Leave empty to use the parent Program's full base population."
                                RepeatDirection="Vertical" />
                        </div>
                        <div class="col-md-6">
                            <Rock:DataViewItemPicker ID="dvpAdditionalDataView" runat="server" Label="Additional Data View Filter"
                                Help="Optional. Further narrows the population beyond the parent group's filters and any prerequisite scoping." />
                        </div>
                    </div>

                    <div class="actions">
                        <asp:LinkButton ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary" OnClick="btnSave_Click" />
                        <asp:LinkButton ID="btnCancel" runat="server" Text="Cancel" CssClass="btn btn-link" CausesValidation="false" OnClick="btnCancel_Click" />
                    </div>
                </asp:Panel>
            </div>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
