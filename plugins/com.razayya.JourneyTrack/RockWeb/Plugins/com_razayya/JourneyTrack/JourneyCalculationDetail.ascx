<%@ Control Language="C#" AutoEventWireup="true" CodeFile="JourneyCalculationDetail.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.JourneyCalculationDetail" %>
<%@ Register TagPrefix="jt" TagName="FilterConditionsEditor"
    Src="~/Plugins/com_razayya/JourneyTrack/Controls/FilterConditionsEditor.ascx" %>
<%@ Register TagPrefix="jt" TagName="CompletionCriteriaEditor"
    Src="~/Plugins/com_razayya/JourneyTrack/Controls/CompletionCriteriaEditor.ascx" %>

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
                        <asp:LinkButton ID="btnCopy" runat="server" Text="Copy" CssClass="btn btn-default btn-sm"
                            OnClick="btnCopy_Click" CausesValidation="false"
                            ToolTip="Create a copy of this Calculation." />
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
                                SourceTypeName="com.razayya.JourneyTrack.Model.JourneyCalculation, com.razayya.JourneyTrack"
                                PropertyName="Name" />
                        </div>
                        <div class="col-md-3">
                            <Rock:RockCheckBox ID="cbIsActive" runat="server" Label="Active" />
                        </div>
                    </div>

                    <Rock:DataTextBox ID="tbDescription" runat="server" Label="Description" TextMode="MultiLine" Rows="3"
                        SourceTypeName="com.razayya.JourneyTrack.Model.JourneyCalculation, com.razayya.JourneyTrack"
                        PropertyName="Description" />

                    <hr />
                    <h4>Calculation Configuration</h4>

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:RockDropDownList ID="cpCalculationType" runat="server" Label="Calculation Type"
                                Required="true" AutoPostBack="true" OnSelectedIndexChanged="cpCalculationType_SelectedIndexChanged" />
                        </div>
                        <div class="col-md-6">
                            <Rock:RockDropDownList ID="apTargetAttribute" runat="server" Label="Target Person Attribute"
                                Required="true" EnhanceForLongLists="true"
                                Help="The Person Attribute that this Calculation will write its result to." />
                        </div>
                    </div>

                    <%-- Valid target attribute categories --%>
                    <asp:Panel ID="pnlValidCategories" runat="server" Visible="false">
                        <div class="panel panel-default">
                            <div class="panel-heading" role="button" data-toggle="collapse" data-target="#collapseValidCategories" aria-expanded="false" style="cursor: pointer;">
                                <h5 class="panel-title">
                                    <i class="fa fa-filter"></i> Valid Target Attribute Categories
                                    <i class="fa fa-chevron-right pull-right js-cat-toggle-icon" style="transition: transform 0.2s;"></i>
                                </h5>
                            </div>
                            <div id="collapseValidCategories" class="panel-collapse collapse">
                                <div class="panel-body">
                                    <asp:Literal ID="lValidCategories" runat="server" />
                                </div>
                            </div>
                        </div>
                        <script>
                            $(function () {
                                $('#collapseValidCategories').on('show.bs.collapse', function () {
                                    $(this).parent().find('.js-cat-toggle-icon').css('transform', 'rotate(90deg)');
                                }).on('hide.bs.collapse', function () {
                                    $(this).parent().find('.js-cat-toggle-icon').css('transform', 'rotate(0deg)');
                                });
                            });
                        </script>
                    </asp:Panel>

                    <%-- Dynamic attributes for the selected JourneyCalculation type component --%>
                    <asp:Panel ID="pnlComponentAttributes" runat="server" CssClass="well">
                        <h5><asp:Literal ID="lComponentName" runat="server" Text="Component Settings" /></h5>
                        <Rock:DynamicPlaceHolder ID="phComponentAttributes" runat="server" />

                        <%-- Visual editors that replace the JSON textarea for known JSON-input attrs --%>
                        <jt:FilterConditionsEditor   ID="fcEditor" runat="server" Visible="false" />
                        <jt:CompletionCriteriaEditor ID="ccEditor" runat="server" Visible="false" />
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
                                Help="What happens when a person does not match the Calculation criteria." />
                            <asp:Panel ID="pnlNoMatchLava" runat="server" Visible="false">
                                <Rock:CodeEditor ID="ceNoMatchLava" runat="server" Label="No Match Lava Template"
                                    EditorMode="Lava" EditorTheme="Rock" EditorHeight="80"
                                    Help="Lava template for the value written when the person does not match." />
                            </asp:Panel>
                            <Rock:RockCheckBox ID="cbSkipIfTargetHasValue" runat="server" Label="Skip If Target Has Value"
                                Help="When enabled, the engine skips this calculation entirely for any person whose target Person Attribute already has a non-blank value. 'Write once, then leave alone' semantics. Big perf win when most people are already done." />
                            <Rock:RockDropDownList ID="ddlOnMatchCommunication" runat="server" Label="On Match Communication"
                                EnhanceForLongLists="true"
                                Help="Optional. When a person transitions from no-match to match for this calculation, queue this SystemCommunication. The Send Journey Communications job dispatches queued rows via Rock's standard delivery (one row per (calc, person), so a given person is notified at most once per calc)." />
                        </div>
                    </div>

                    <%-- Merge field documentation --%>
                    <asp:Panel ID="pnlMergeFields" runat="server" Visible="false">
                        <div class="panel panel-default margin-t-md">
                            <div class="panel-heading" role="button" data-toggle="collapse" data-target="#collapseMergeFields" aria-expanded="false" style="cursor: pointer;">
                                <h5 class="panel-title">
                                    <i class="fa fa-code"></i> Available Merge Fields
                                    <i class="fa fa-chevron-right pull-right js-toggle-icon" style="transition: transform 0.2s;"></i>
                                </h5>
                            </div>
                            <div id="collapseMergeFields" class="panel-collapse collapse">
                                <div class="panel-body">
                                    <asp:Literal ID="lMergeFields" runat="server" />
                                </div>
                            </div>
                        </div>
                        <script>
                            $(function () {
                                $('#collapseMergeFields').on('show.bs.collapse', function () {
                                    $(this).parent().find('.js-toggle-icon').css('transform', 'rotate(90deg)');
                                }).on('hide.bs.collapse', function () {
                                    $(this).parent().find('.js-toggle-icon').css('transform', 'rotate(0deg)');
                                });
                            });
                        </script>
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
                                    Help="Optional. Select a person to preview/execute this Calculation for just that individual." />
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
