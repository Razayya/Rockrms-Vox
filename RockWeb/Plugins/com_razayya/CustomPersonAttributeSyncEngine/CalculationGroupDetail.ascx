<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CalculationGroupDetail.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine.CalculationGroupDetail" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDetails" CssClass="panel panel-block" runat="server">
            <asp:HiddenField ID="hfId" runat="server" />

            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-sync"></i>
                    <asp:Literal ID="lTitle" runat="server" />
                </h1>
                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlInactive" runat="server" LabelType="Danger" Text="Inactive" Visible="false" />
                    <Rock:HighlightLabel ID="hlLastRun" runat="server" LabelType="Info" />
                </div>
            </div>

            <Rock:PanelDrawer ID="pdAuditDetails" runat="server" />

            <div class="panel-body">
                <Rock:NotificationBox ID="nbWarning" runat="server" NotificationBoxType="Warning" Visible="false" />
                <Rock:NotificationBox ID="nbEditModeMessage" runat="server" NotificationBoxType="Info" />

                <%-- View Mode --%>
                <asp:Panel ID="pnlView" runat="server">
                    <div class="row">
                        <div class="col-md-6">
                            <asp:Literal ID="lDescription" runat="server" />
                        </div>
                        <div class="col-md-6">
                            <dl>
                                <asp:Literal ID="lPopulationSummary" runat="server" />
                            </dl>
                        </div>
                    </div>

                    <div class="actions">
                        <asp:LinkButton ID="btnEdit" runat="server" Text="Edit" CssClass="btn btn-primary" OnClick="btnEdit_Click" CausesValidation="false" />
                        <asp:LinkButton ID="btnCopy" runat="server" Text="Copy" CssClass="btn btn-default btn-sm" OnClick="btnCopy_Click" CausesValidation="false"
                            ToolTip="Create a deep copy of this group including all sub-groups and calculations." />
                        <asp:LinkButton ID="btnExport" runat="server" Text="Export" CssClass="btn btn-default btn-sm" OnClick="btnExport_Click" CausesValidation="false"
                            ToolTip="Download this group configuration as a portable JSON file." />
                        <Rock:SecurityButton ID="btnSecurity" runat="server" class="btn btn-sm btn-square btn-security pull-right" />
                    </div>

                    <hr />

                    <%-- SubGroup List --%>
                    <h4>Calculation Sub Groups</h4>
                    <div class="grid grid-panel">
                        <Rock:Grid ID="gSubGroups" runat="server" RowItemText="Sub Group" AllowSorting="false"
                            OnRowSelected="gSubGroups_RowSelected" DisplayType="Light">
                            <Columns>
                                <Rock:ReorderField />
                                <Rock:RockBoundField DataField="Name" HeaderText="Name" />
                                <Rock:RockBoundField DataField="CalculationCount" HeaderText="Calculations"
                                    ItemStyle-HorizontalAlign="Center" HeaderStyle-HorizontalAlign="Center" />
                                <Rock:BoolField DataField="ScopeToPreviousSubGroup" HeaderText="Scoped to Previous" />
                                <Rock:BoolField DataField="IsActive" HeaderText="Active" />
                                <Rock:DeleteField OnClick="gSubGroups_Delete" />
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
                                SourceTypeName="com.razayya.CustomPersonAttributeSyncEngine.Model.CalculationGroup, com.razayya.CustomPersonAttributeSyncEngine"
                                PropertyName="Name" />
                        </div>
                        <div class="col-md-6">
                            <Rock:RockCheckBox ID="cbIsActive" runat="server" Label="Active" />
                        </div>
                    </div>

                    <Rock:DataTextBox ID="tbDescription" runat="server" Label="Description" TextMode="MultiLine" Rows="3"
                        SourceTypeName="com.razayya.CustomPersonAttributeSyncEngine.Model.CalculationGroup, com.razayya.CustomPersonAttributeSyncEngine"
                        PropertyName="Description" />

                    <h4>Population Filters</h4>
                    <p class="text-muted">Define the base population of people this group will evaluate. Leave blank to include everyone.</p>

                    <div class="row">
                        <div class="col-md-4">
                            <Rock:DefinedValuePicker ID="dvpRecordStatus" runat="server" Label="Record Status" />
                        </div>
                        <div class="col-md-4">
                            <Rock:DefinedValuePicker ID="dvpConnectionStatus" runat="server" Label="Connection Status" />
                        </div>
                        <div class="col-md-4">
                            <Rock:CampusPicker ID="cpCampus" runat="server" Label="Campus" />
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6">
                            <Rock:DataViewItemPicker ID="dvpDataView" runat="server" Label="Data View"
                                Help="Optional. Only people in this Data View will be included in the base population." />
                        </div>
                    </div>

                    <h4>Target Attribute Categories</h4>
                    <p class="text-muted">Restrict which Person Attribute categories are available as targets for calculations in this group. Leave blank to allow all.</p>
                    <div class="row">
                        <div class="col-md-6">
                            <Rock:CategoryPicker ID="cpAttributeCategories" runat="server" Label="Person Attribute Categories"
                                AllowMultiSelect="true"
                                Help="Select one or more Person Attribute categories. Only attributes in these categories will be available as targets when configuring calculations." />
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
