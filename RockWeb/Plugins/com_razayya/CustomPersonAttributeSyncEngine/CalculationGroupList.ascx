<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CalculationGroupList.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine.CalculationGroupList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-sync"></i>
                    Attribute Sync Engine
                </h1>
            </div>
            <div class="panel-body">
                <div class="grid grid-panel">
                    <Rock:ModalAlert ID="mdGridWarning" runat="server" />
                    <Rock:Grid ID="gList" runat="server" RowItemText="Calculation Group"
                        AllowSorting="true" OnRowSelected="gList_RowSelected" TooltipField="Description">
                        <Columns>
                            <Rock:ReorderField />
                            <Rock:RockBoundField DataField="Name" HeaderText="Name" SortExpression="Name" />
                            <Rock:RockBoundField DataField="Description" HeaderText="Description" SortExpression="Description"
                                TruncateLength="80" />
                            <Rock:RockBoundField DataField="SubGroupCount" HeaderText="Sub Groups" SortExpression="SubGroupCount"
                                ItemStyle-HorizontalAlign="Center" HeaderStyle-HorizontalAlign="Center" />
                            <Rock:DateTimeField DataField="LastRunDateTime" HeaderText="Last Run" SortExpression="LastRunDateTime" />
                            <Rock:BoolField DataField="IsActive" HeaderText="Active" SortExpression="IsActive" />
                            <Rock:SecurityField TitleField="Name" />
                            <Rock:DeleteField OnClick="gList_Delete" />
                        </Columns>
                    </Rock:Grid>
                </div>
            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
