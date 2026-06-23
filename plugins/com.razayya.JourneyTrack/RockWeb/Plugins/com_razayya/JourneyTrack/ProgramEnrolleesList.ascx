<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ProgramEnrolleesList.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.ProgramEnrolleesList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-users"></i>
                    Enrollees
                    <small class="text-muted js-program-name">
                        <asp:Literal ID="lProgramName" runat="server" />
                    </small>
                </h1>
                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlActiveCount" runat="server" LabelType="Info" />
                </div>
            </div>
            <div class="panel-body">
                <Rock:NotificationBox ID="nbResult" runat="server" Visible="false" />

                <div class="row margin-b-md">
                    <div class="col-md-9">
                        <Rock:PersonPicker ID="ppNewEnrollee" runat="server" Label="Add Enrollee" />
                    </div>
                    <div class="col-md-3" style="padding-top: 24px;">
                        <asp:LinkButton ID="btnEnroll" runat="server" CssClass="btn btn-primary btn-sm"
                            OnClick="btnEnroll_Click" CausesValidation="false">
                            <i class="fa fa-plus"></i> Enroll
                        </asp:LinkButton>
                        <asp:LinkButton ID="btnBack" runat="server" CssClass="btn btn-link btn-sm"
                            OnClick="btnBack_Click" CausesValidation="false" Text="Back to Program" />
                    </div>
                </div>

                <div class="grid-filter">
                    <Rock:RockDropDownList ID="ddlStatus" runat="server" Label="Status"
                        AutoPostBack="true" OnSelectedIndexChanged="ddlStatus_SelectedIndexChanged">
                        <asp:ListItem Text="Active only"   Value="active" Selected="True" />
                        <asp:ListItem Text="Inactive only" Value="inactive" />
                        <asp:ListItem Text="All"           Value="all" />
                    </Rock:RockDropDownList>
                    <Rock:RockDropDownList ID="ddlCampus" runat="server" Label="Campus"
                        AutoPostBack="true" OnSelectedIndexChanged="ddlStatus_SelectedIndexChanged" />
                    <Rock:RockTextBox ID="tbSourceFilter" runat="server" Label="Source contains" />
                    <asp:LinkButton ID="btnFilter" runat="server" CssClass="btn btn-action btn-xs"
                        OnClick="btnFilter_Click" Text="Filter" />
                </div>

                <div class="grid grid-panel">
                    <Rock:ModalAlert ID="mdAlert" runat="server" />
                    <Rock:Grid ID="gEnrollees" runat="server" RowItemText="Enrollee"
                        AllowSorting="true" AllowPaging="true" OnGridRebind="gEnrollees_GridRebind">
                        <Columns>
                            <Rock:RockBoundField DataField="PersonName" HeaderText="Person" SortExpression="PersonName" />
                            <Rock:RockBoundField DataField="Campus" HeaderText="Campus" SortExpression="Campus" />
                            <Rock:DateTimeField DataField="EnrolledDateTime" HeaderText="Enrolled" SortExpression="EnrolledDateTime" />
                            <Rock:RockBoundField DataField="Source" HeaderText="Source" SortExpression="Source"
                                ItemStyle-HorizontalAlign="Center" HeaderStyle-HorizontalAlign="Center" />
                            <Rock:BoolField DataField="IsActive" HeaderText="Active" SortExpression="IsActive" />
                            <Rock:DateTimeField DataField="UnenrolledDateTime" HeaderText="Unenrolled" SortExpression="UnenrolledDateTime" />
                            <Rock:RockTemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnUnenroll" runat="server" CssClass="btn btn-warning btn-xs"
                                        CommandName="Unenroll" CommandArgument='<%# Eval("Id") %>'
                                        Visible='<%# (bool)Eval("IsActive") %>'
                                        OnClick="btnUnenroll_Click"
                                        ToolTip="Unenroll this person"
                                        CausesValidation="false">
                                        <i class="fa fa-user-minus"></i> Unenroll
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
