<%@ Control Language="C#" AutoEventWireup="true" CodeFile="RsvpGroupExclusions.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.RSVPReminders.RsvpGroupExclusions" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <%-- Self-gating: hidden unless the route group is AutoRSVP-enabled and the
             current person can manage it. --%>
        <asp:Panel ID="pnlExclusions" runat="server" Visible="false" CssClass="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-calendar-times-o"></i> Meeting Exclusions</h1>
            </div>
            <div class="panel-body">
                <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />
                <p class="text-muted">Skip a date your group won't be meeting and RSVP emails won't go out for it. You can un-skip a date any time before it arrives.</p>

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
                                    <span><%# ( (DateTime) Container.DataItem ).ToString( "ddd, MMM d, yyyy" ) %></span>
                                    <asp:LinkButton runat="server" CssClass="btn btn-link btn-xs"
                                        CommandName="Remove" CommandArgument='<%# ( (DateTime) Container.DataItem ).ToString( "yyyy-MM-dd" ) %>'
                                        CausesValidation="false">Un-skip</asp:LinkButton>
                                </li>
                            </ItemTemplate>
                            <FooterTemplate></ul></FooterTemplate>
                        </asp:Repeater>
                        <asp:Literal ID="lNoExclusions" runat="server" Visible="false"
                            Text="<p class='text-muted small'>No skipped dates.</p>" />

                        <div class="margin-t-md">
                            <Rock:DateRangePicker ID="drpSkipRange" runat="server" Label="Skip a date range"
                                Help="Skips every meeting that falls between the two dates (inclusive). Leave the second date blank to skip a single date." />
                            <asp:LinkButton ID="lbAddRange" runat="server" CssClass="btn btn-default btn-sm"
                                OnClick="lbAddRange_Click" CausesValidation="false">Skip Dates</asp:LinkButton>
                        </div>
                    </div>
                </div>
            </div>
        </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
