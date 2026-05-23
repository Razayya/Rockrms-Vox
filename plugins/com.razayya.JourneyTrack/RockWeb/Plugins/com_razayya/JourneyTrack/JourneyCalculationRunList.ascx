<%@ Control Language="C#" AutoEventWireup="true" CodeFile="JourneyCalculationRunList.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.JourneyCalculationRunList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-history"></i>
                    Run History
                </h1>
            </div>
            <div class="panel-body">
                <Rock:NotificationBox ID="nbResult" runat="server" Visible="false" />

                <div class="grid-filter">
                    <Rock:RockDropDownList ID="ddlCalculation" runat="server" Label="JourneyCalculation"
                        EnhanceForLongLists="true" AutoPostBack="true" OnSelectedIndexChanged="ddlCalculation_SelectedIndexChanged" />
                    <Rock:SlidingDateRangePicker ID="drpDateRange" runat="server" Label="Date Range" />
                    <Rock:RockDropDownList ID="ddlStatus" runat="server" Label="Status">
                        <asp:ListItem Text="" Value="" />
                        <asp:ListItem Text="Success" Value="True" />
                        <asp:ListItem Text="Failed" Value="False" />
                    </Rock:RockDropDownList>
                    <asp:LinkButton ID="btnFilter" runat="server" CssClass="btn btn-action btn-xs"
                        OnClick="btnFilter_Click" Text="Filter" />
                </div>
                <div class="grid grid-panel">
                    <Rock:Grid ID="gRunHistory" runat="server" RowItemText="Run"
                        AllowSorting="true" AllowPaging="true" OnGridRebind="gRunHistory_GridRebind">
                        <Columns>
                            <Rock:RockBoundField DataField="CalculationName" HeaderText="JourneyCalculation" SortExpression="CalculationName" />
                            <Rock:DateTimeField DataField="RunDateTime" HeaderText="Started" SortExpression="RunDateTime" />
                            <Rock:DateTimeField DataField="CompletedDateTime" HeaderText="Completed" SortExpression="CompletedDateTime" />
                            <Rock:RockBoundField DataField="RunByPersonName" HeaderText="Run By" SortExpression="RunByPersonName" />
                            <Rock:RockBoundField DataField="PopulationCount" HeaderText="Population"
                                ItemStyle-HorizontalAlign="Right" HeaderStyle-HorizontalAlign="Right" SortExpression="PopulationCount" />
                            <Rock:RockBoundField DataField="MatchedCount" HeaderText="Matched"
                                ItemStyle-HorizontalAlign="Right" HeaderStyle-HorizontalAlign="Right" SortExpression="MatchedCount" />
                            <Rock:RockBoundField DataField="UpdatedCount" HeaderText="Updated"
                                ItemStyle-HorizontalAlign="Right" HeaderStyle-HorizontalAlign="Right" SortExpression="UpdatedCount" />
                            <Rock:RockBoundField DataField="SkippedCount" HeaderText="Skipped"
                                ItemStyle-HorizontalAlign="Right" HeaderStyle-HorizontalAlign="Right" SortExpression="SkippedCount" />
                            <Rock:RockBoundField DataField="ErrorCount" HeaderText="Errors"
                                ItemStyle-HorizontalAlign="Right" HeaderStyle-HorizontalAlign="Right" SortExpression="ErrorCount" />
                            <Rock:BoolField DataField="WasSuccessful" HeaderText="Success" SortExpression="WasSuccessful" />
                            <Rock:RockTemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnRetry" runat="server" CssClass="btn btn-warning btn-sm"
                                        CommandName="RetryRun" CommandArgument='<%# Eval("Id") %>'
                                        Visible='<%# !(bool)Eval("WasSuccessful") %>'
                                        OnClick="btnRetry_Click"
                                        ToolTip="Reprocess this JourneyCalculation"
                                        CausesValidation="false">
                                        <i class="fa fa-redo"></i> Retry
                                    </asp:LinkButton>
                                </ItemTemplate>
                            </Rock:RockTemplateField>
                        </Columns>
                    </Rock:Grid>
                </div>
            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
