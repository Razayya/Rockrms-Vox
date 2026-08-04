<%@ Control Language="C#" AutoEventWireup="true" CodeFile="RsvpGroupExclusions.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.RSVPReminders.RsvpGroupExclusions" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <%-- Link mode: rendered when this block instance sits on the Group Toolbox.
             Self-gating: hidden unless the route group is AutoRSVP-enabled and the
             current person can manage it. --%>
        <asp:Panel ID="pnlLink" runat="server" Visible="false" CssClass="margin-b-md">
            <asp:HyperLink ID="hlManage" runat="server" CssClass="btn btn-default btn-sm">
                <i class="fa fa-calendar-times-o"></i> Skip Meeting Dates
            </asp:HyperLink>
        </asp:Panel>

        <%-- Manager mode: rendered on the dedicated Meeting Exclusions page. --%>
        <asp:Panel ID="pnlExclusions" runat="server" Visible="false" CssClass="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-calendar-times-o"></i> Meeting Exclusions</h1>
            </div>
            <div class="panel-body">
                <asp:HyperLink ID="hlBack" runat="server" Visible="false" CssClass="btn btn-link btn-xs margin-b-sm">
                    <i class="fa fa-chevron-left"></i> Back to Group Toolbox
                </asp:HyperLink>
                <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />
                <asp:Literal ID="lIntro" runat="server" />

                <div class="row">
                    <div class="col-md-6">
                        <h5>Upcoming Meetings</h5>
                        <asp:Repeater ID="rptUpcoming" runat="server" OnItemCommand="rptUpcoming_ItemCommand">
                            <HeaderTemplate><ul class="list-group"></HeaderTemplate>
                            <ItemTemplate>
                                <li class="list-group-item" style="display:flex;justify-content:space-between;align-items:center;">
                                    <span><%# ( (DateTime) Container.DataItem ).ToString( "ddd, MMM d, yyyy" ) %></span>
                                    <asp:LinkButton runat="server" CssClass="btn btn-default btn-xs"
                                        CommandName="Skip" CommandArgument='<%# ( (DateTime) Container.DataItem ).ToString( "yyyy-MM-dd" ) %>'
                                        CausesValidation="false">Skip</asp:LinkButton>
                                </li>
                            </ItemTemplate>
                            <FooterTemplate></ul></FooterTemplate>
                        </asp:Repeater>
                        <asp:Literal ID="lNoUpcoming" runat="server" Visible="false"
                            Text="<p class='text-muted small'>No upcoming meeting dates found.</p>" />
                    </div>
                    <div class="col-md-6">
                        <h5>Skipped Dates</h5>
                        <asp:Repeater ID="rptExclusions" runat="server" OnItemCommand="rptExclusions_ItemCommand">
                            <HeaderTemplate><ul class="list-group"></HeaderTemplate>
                            <ItemTemplate>
                                <li class="list-group-item" style="display:flex;justify-content:space-between;align-items:center;">
                                    <span><%# Eval( "Label" ) %></span>
                                    <asp:LinkButton runat="server" CssClass="btn btn-link btn-xs"
                                        CommandName="Remove" CommandArgument='<%# Eval( "Arg" ) %>'
                                        CausesValidation="false">Un-skip</asp:LinkButton>
                                </li>
                            </ItemTemplate>
                            <FooterTemplate></ul></FooterTemplate>
                        </asp:Repeater>
                        <asp:Literal ID="lNoExclusions" runat="server" Visible="false"
                            Text="<p class='text-muted small'>No skipped dates.</p>" />

                        <div class="margin-t-md">
                            <Rock:DateRangePicker ID="drpSkipRange" runat="server" Label="Skip a date range" />
                            <asp:LinkButton ID="lbAddRange" runat="server" CssClass="btn btn-default btn-sm"
                                OnClick="lbAddRange_Click" CausesValidation="false">Skip Dates</asp:LinkButton>
                        </div>
                    </div>
                </div>
            </div>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
