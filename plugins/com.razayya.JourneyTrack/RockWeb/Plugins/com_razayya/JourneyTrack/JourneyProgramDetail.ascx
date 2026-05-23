<%@ Control Language="C#" AutoEventWireup="true" CodeFile="JourneyProgramDetail.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.JourneyProgramDetail" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <Triggers>
        <asp:PostBackTrigger ControlID="btnExport" />
    </Triggers>
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
                        <asp:LinkButton ID="btnEnrollees" runat="server" Text="Manage Enrollees" CssClass="btn btn-default btn-sm" OnClick="btnEnrollees_Click" CausesValidation="false"
                            Visible="false" ToolTip="View / manage the people enrolled in this program." />
                        <asp:LinkButton ID="btnReconcile" runat="server" Text="Reconcile Now" CssClass="btn btn-default btn-sm" OnClick="btnReconcile_Click" CausesValidation="false"
                            Visible="false" ToolTip="Run an auto-enroll reconciliation pass against the configured population spec." />
                        <asp:LinkButton ID="btnCopy" runat="server" Text="Copy" CssClass="btn btn-default btn-sm" OnClick="btnCopy_Click" CausesValidation="false"
                            ToolTip="Create a deep copy of this group including all sub-groups and calculations." />
                        <asp:LinkButton ID="btnExport" runat="server" Text="Export" CssClass="btn btn-default btn-sm" OnClick="btnExport_Click" CausesValidation="false"
                            ToolTip="Download this group configuration as a portable JSON file." />
                        <Rock:SecurityButton ID="btnSecurity" runat="server" class="btn btn-sm btn-square btn-security pull-right" />
                    </div>

                    <hr />

                    <%-- Stage List --%>
                    <h4>Stages</h4>
                    <div class="grid grid-panel">
                        <Rock:Grid ID="gSubGroups" runat="server" RowItemText="Stage" AllowSorting="false"
                            OnRowSelected="gSubGroups_RowSelected" DisplayType="Light">
                            <Columns>
                                <Rock:ReorderField />
                                <Rock:RockBoundField DataField="Name" HeaderText="Name" />
                                <Rock:RockBoundField DataField="CalculationCount" HeaderText="Calculations"
                                    ItemStyle-HorizontalAlign="Center" HeaderStyle-HorizontalAlign="Center" />
                                <Rock:BoolField DataField="HasPrerequisites" HeaderText="Has Prerequisites" />
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
                                SourceTypeName="com.razayya.JourneyTrack.Model.JourneyProgram, com.razayya.JourneyTrack"
                                PropertyName="Name" />
                        </div>
                        <div class="col-md-6">
                            <Rock:RockCheckBox ID="cbIsActive" runat="server" Label="Active" />
                        </div>
                    </div>

                    <Rock:DataTextBox ID="tbDescription" runat="server" Label="Description" TextMode="MultiLine" Rows="3"
                        SourceTypeName="com.razayya.JourneyTrack.Model.JourneyProgram, com.razayya.JourneyTrack"
                        PropertyName="Description" />

                    <h4>Enrollment</h4>
                    <p class="text-muted">Controls how people enter and leave this Program.</p>

                    <div class="row">
                        <div class="col-md-4">
                            <Rock:RockCheckBox ID="cbRequiresEnrollment" runat="server" Label="Requires Enrollment"
                                AutoPostBack="true" OnCheckedChanged="cbRequiresEnrollment_CheckedChanged"
                                Help="When enabled, the engine evaluates only people in the Enrollment table for this Program (not the full Rock Person table). Population filters below become an auto-enroll spec rather than a runtime filter." />
                        </div>
                        <div class="col-md-4">
                            <Rock:RockCheckBox ID="cbAutoEnroll" runat="server" Label="Auto-Enroll from Population"
                                Help="When enabled (alongside Requires Enrollment), the nightly job + a manual 'Reconcile Now' button add anyone matching the Population filters below into the Enrollment table." />
                        </div>
                        <div class="col-md-4">
                            <Rock:RockCheckBox ID="cbAutoUnenroll" runat="server" Label="Auto-Unenroll on Population Leave"
                                Help="When enabled, reconciliation also soft-unenrolls people who no longer match the population spec (e.g., changed campus). When disabled, auto-enroll only adds." />
                        </div>
                    </div>

                    <h4>
                        <asp:Literal ID="lPopulationHeading" runat="server" Text="Population Filters" />
                    </h4>
                    <p class="text-muted">
                        <asp:Literal ID="lPopulationHelp" runat="server" Text="Define the base population of people this group will evaluate. Leave blank to include everyone." />
                    </p>

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
                                Help="Optional. Only people in this Data View are included." />
                        </div>
                    </div>

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:RockDropDownList ID="ddlOnCompleteCommunication" runat="server" Label="On Complete Communication"
                                EnhanceForLongLists="true"
                                Help="Optional. When a person's rollup attribute transitions to True (completing every Stage), queue this SystemCommunication. Each person is notified at most once per program." />
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
