<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CalculationTreeView.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine.CalculationTreeView" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-sitemap"></i>
                    <asp:Literal ID="lGroupName" runat="server" Text="Navigation" />
                </h1>
            </div>
            <div class="panel-body" style="padding: 0;">
                <asp:Literal ID="lTreeHtml" runat="server" />
            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
