<%@ Control Language="C#" AutoEventWireup="true" CodeFile="PersonJourneyProgress.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.PersonJourneyProgress" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />
        <asp:Literal ID="lOutput" runat="server" />
    </ContentTemplate>
</asp:UpdatePanel>
