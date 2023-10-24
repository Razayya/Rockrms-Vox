<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ProjectDetailProjectBoardList.ascx.cs" Inherits="RockWeb.Plugins.com_razayya.Blocks.ProjectManagement.ProjectDetailProjectBoardList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlProjectBoardList" runat="server" CssClass="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <asp:Literal ID="ltHtmlIcon" runat="server" />
                    <span>Project Boards</span>
                </h1>
            </div>
             <ul class="list-group pm-list-group">
            <asp:Repeater ID="rpProjectBoard" runat="server" OnItemDataBound="rpProjectBoard_ItemDataBound" OnItemCommand="rpProjectBoard_ItemCommand">
                <ItemTemplate>
                    <li id="liProject" runat="server" class="list-group-item pm-group-item" data-toggle="tooltip" data-delay="600" data-placement="auto left">
                        <div class="pm-group-item-heading clearfix">
                            <asp:LinkButton ID="lbProjectBoardName" runat="server" CssClass="pm-group-item-name" CommandName="ToggleProjectBoard" CommandArgument='<%# Eval( "Id" ) %>'></asp:LinkButton>
                        </div>
                    </li>
                </ItemTemplate>
            </asp:Repeater>
        </ul>
        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>

