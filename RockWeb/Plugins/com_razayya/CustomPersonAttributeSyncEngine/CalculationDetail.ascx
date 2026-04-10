<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CalculationDetail.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine.CalculationDetail" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDetails" CssClass="panel panel-block" runat="server">
            <asp:HiddenField ID="hfId" runat="server" />
            <asp:HiddenField ID="hfSubGroupId" runat="server" />

            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-calculator"></i>
                    <asp:Literal ID="lTitle" runat="server" />
                </h1>
                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlInactive" runat="server" LabelType="Danger" Text="Inactive" Visible="false" />
                    <Rock:HighlightLabel ID="hlCalcType" runat="server" LabelType="Type" />
                </div>
            </div>

            <Rock:PanelDrawer ID="pdAuditDetails" runat="server" />

            <div class="panel-body">
                <Rock:NotificationBox ID="nbWarning" runat="server" NotificationBoxType="Warning" Visible="false" />

                <%-- View Mode --%>
                <asp:Panel ID="pnlView" runat="server">
                    <div class="row">
                        <div class="col-md-6">
                            <asp:Literal ID="lViewDescription" runat="server" />
                            <dl>
                                <asp:Literal ID="lViewDetails" runat="server" />
                            </dl>
                        </div>
                        <div class="col-md-6">
                            <dl>
                                <asp:Literal ID="lViewConfig" runat="server" />
                            </dl>
                        </div>
                    </div>

                    <div class="actions">
                        <asp:LinkButton ID="btnEdit" runat="server" Text="Edit" CssClass="btn btn-primary"
                            OnClick="btnEdit_Click" CausesValidation="false" />
                        <asp:LinkButton ID="btnPlay" runat="server" Text="<i class='fa fa-play'></i> Preview"
                            CssClass="btn btn-default" OnClick="btnPlay_Click" CausesValidation="false" />
                        <asp:LinkButton ID="btnBack" runat="server" Text="Back" CssClass="btn btn-link"
                            OnClick="btnBack_Click" CausesValidation="false" />
                    </div>
                </asp:Panel>

                <%-- Edit Mode --%>
                <asp:Panel ID="pnlEdit" runat="server" Visible="false">
                    <asp:ValidationSummary ID="valSummary" runat="server"
                        HeaderText="Please correct the following:" CssClass="alert alert-validation" />

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:DataTextBox ID="tbName" runat="server" Label="Name" Required="true"
                                SourceTypeName="com.razayya.CustomPersonAttributeSyncEngine.Model.Calculation, com.razayya.CustomPersonAttributeSyncEngine"
                                PropertyName="Name" />
                        </div>
                        <div class="col-md-3">
                            <Rock:RockCheckBox ID="cbIsActive" runat="server" Label="Active" />
                        </div>
                    </div>

                    <Rock:DataTextBox ID="tbDescription" runat="server" Label="Description" TextMode="MultiLine" Rows="3"
                        SourceTypeName="com.razayya.CustomPersonAttributeSyncEngine.Model.Calculation, com.razayya.CustomPersonAttributeSyncEngine"
                        PropertyName="Description" />

                    <hr />
                    <h4>Calculation Configuration</h4>

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:ComponentPicker ID="cpCalculationType" runat="server" Label="Calculation Type"
                                ContainerType="com.razayya.CustomPersonAttributeSyncEngine.CalculationTypes.CalculationTypeContainer, com.razayya.CustomPersonAttributeSyncEngine"
                                Required="true" AutoPostBack="true" OnSelectedIndexChanged="cpCalculationType_SelectedIndexChanged" />
                        </div>
                        <div class="col-md-6">
                            <Rock:AttributePicker ID="apTargetAttribute" runat="server" Label="Target Person Attribute"
                                AllowMulti="false" Required="true"
                                Help="The Person Attribute that this calculation will write its result to." />
                        </div>
                    </div>

                    <%-- Dynamic attributes for the selected calculation type component --%>
                    <asp:Panel ID="pnlComponentAttributes" runat="server" CssClass="well">
                        <h5><asp:Literal ID="lComponentName" runat="server" Text="Component Settings" /></h5>
                        <Rock:DynamicPlaceHolder ID="phComponentAttributes" runat="server" />
                    </asp:Panel>

                    <hr />
                    <h4>Result Configuration</h4>

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:CodeEditor ID="ceResultLava" runat="server" Label="Result Lava Template"
                                EditorMode="Lava" EditorTheme="Rock" EditorHeight="120"
                                Help="Lava template to produce the value written to the target attribute when the person matches. Available merge fields depend on the Calculation Type." />
                        </div>
                        <div class="col-md-6">
                            <Rock:RockDropDownList ID="ddlNoMatchBehavior" runat="server" Label="No Match Behavior"
                                AutoPostBack="true" OnSelectedIndexChanged="ddlNoMatchBehavior_SelectedIndexChanged"
                                Help="What happens when a person does not match the calculation criteria." />
                            <asp:Panel ID="pnlNoMatchLava" runat="server" Visible="false">
                                <Rock:CodeEditor ID="ceNoMatchLava" runat="server" Label="No Match Lava Template"
                                    EditorMode="Lava" EditorTheme="Rock" EditorHeight="80"
                                    Help="Lava template for the value written when the person does not match." />
                            </asp:Panel>
                        </div>
                    </div>

                    <%-- Merge field documentation --%>
                    <asp:Panel ID="pnlMergeFields" runat="server" Visible="false" CssClass="well well-sm">
                        <h5>Available Merge Fields</h5>
                        <asp:Literal ID="lMergeFields" runat="server" />
                    </asp:Panel>

                    <div class="actions">
                        <asp:LinkButton ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary" OnClick="btnSave_Click" />
                        <asp:LinkButton ID="btnCancel" runat="server" Text="Cancel" CssClass="btn btn-link"
                            CausesValidation="false" OnClick="btnCancel_Click" />
                    </div>
                </asp:Panel>

                <%-- Preview/Play Panel --%>
                <asp:Panel ID="pnlPreview" runat="server" Visible="false">
                    <h4><i class="fa fa-play"></i> Calculation Preview</h4>

                    <div class="well well-sm">
                        <div class="row">
                            <div class="col-md-6">
                                <Rock:PersonPicker ID="ppSinglePerson" runat="server" Label="Test Single Person"
                                    Help="Optional. Select a person to preview/execute this calculation for just that individual." />
                            </div>
                            <div class="col-md-6" style="padding-top: 24px;">
                                <asp:LinkButton ID="btnPreviewSinglePerson" runat="server" Text="<i class='fa fa-search'></i> Preview for Person"
                                    CssClass="btn btn-default btn-sm" OnClick="btnPreviewSinglePerson_Click" CausesValidation="false" />
                                <asp:LinkButton ID="btnExecuteSinglePerson" runat="server" Text="<i class='fa fa-bolt'></i> Execute for Person"
                                    CssClass="btn btn-warning btn-sm" OnClick="btnExecuteSinglePerson_Click" CausesValidation="false"
                                    OnClientClick="return Rock.dialogs.confirmPreventOnCancel(event, 'This will write the attribute value for this person. Continue?');" />
                                <asp:LinkButton ID="btnPreviewAll" runat="server" Text="<i class='fa fa-users'></i> Preview Full Population"
                                    CssClass="btn btn-default btn-sm" OnClick="btnPlay_Click" CausesValidation="false" />
                            </div>
                        </div>
                    </div>

                    <Rock:NotificationBox ID="nbPreviewInfo" runat="server" NotificationBoxType="Info" />
                    <Rock:NotificationBox ID="nbExecutionResult" runat="server" NotificationBoxType="Success" Visible="false" />

                    <div class="grid grid-panel">
                        <Rock:Grid ID="gPreview" runat="server" RowItemText="Person" AllowSorting="true"
                            AllowPaging="true" DisplayType="Light">
                            <Columns>
                                <Rock:RockBoundField DataField="PersonName" HeaderText="Person" SortExpression="PersonName" />
                                <Rock:RockBoundField DataField="CurrentValue" HeaderText="Current Value" />
                                <Rock:RockBoundField DataField="NewValue" HeaderText="Projected Value" />
                                <Rock:RockBoundField DataField="Action" HeaderText="Action" />
                            </Columns>
                        </Rock:Grid>
                    </div>

                    <div class="actions">
                        <asp:LinkButton ID="btnExecutePreview" runat="server" Text="<i class='fa fa-bolt'></i> Execute for All in Population"
                            CssClass="btn btn-warning" OnClick="btnExecutePreview_Click"
                            OnClientClick="return Rock.dialogs.confirmPreventOnCancel(event, 'This will write attribute values for the entire population. This cannot be undone. Continue?');" />
                        <asp:LinkButton ID="btnClosePreview" runat="server" Text="Close Preview"
                            CssClass="btn btn-link" CausesValidation="false" OnClick="btnClosePreview_Click" />
                    </div>
                </asp:Panel>
            </div>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
