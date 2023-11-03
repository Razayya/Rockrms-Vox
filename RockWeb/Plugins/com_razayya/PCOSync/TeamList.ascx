<%@ Control Language="C#" AutoEventWireup="true" CodeFile="TeamList.ascx.cs" Inherits="RockWeb.Plugins.com_razayya.Blocks.PCOSync.TeamList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block">
        
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-star"></i> 
                    Planning Center Teams
                </h1>
            </div>
            <div class="panel-body">

                <div class="grid grid-panel">
                    <Rock:Grid ID="gList" runat="server" AllowSorting="true">
                        <Columns>
                            <Rock:RockBoundField DataField="Name" HeaderText="Name" SortExpression="Name" />
                            <Rock:RockBoundField DataField="CreatedAt" HeaderText="Created At" SortExpression="CreatedAt" />
                            <Rock:RockBoundField DataField="Id" HeaderText="Id" SortExpression="Id" />
                            <Rock:RockBoundField DataField="ServiceType" HeaderText="ServiceType" SortExpression="ServiceType" />
                        </Columns>
                    </Rock:Grid>
                </div>

            </div>
        
        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
