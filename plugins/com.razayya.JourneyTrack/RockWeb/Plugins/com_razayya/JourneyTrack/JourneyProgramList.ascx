<%@ Control Language="C#" AutoEventWireup="true" CodeFile="JourneyProgramList.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.JourneyProgramList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-sync"></i>
                    JourneyTrack
                </h1>
            </div>
            <div class="panel-body">
                <asp:LinkButton ID="btnImport" runat="server" CssClass="btn btn-default btn-sm margin-b-md"
                    OnClick="btnImport_Click" CausesValidation="false">
                    <i class="fa fa-upload"></i> Import
                </asp:LinkButton>

                <Rock:ModalDialog ID="mdImport" runat="server" Title="Import Journey Program" OnSaveClick="mdImport_SaveClick" SaveButtonText="Import">
                    <Content>
                        <Rock:NotificationBox ID="nbImportWarning" runat="server" NotificationBoxType="Warning" Visible="false" />
                        <Rock:CodeEditor ID="ceImportJson" runat="server" Label="JSON Configuration"
                            EditorMode="JavaScript" EditorTheme="Rock" EditorHeight="400"
                            Help="Paste the exported JSON configuration for a Journey Program." />
                    </Content>
                </Rock:ModalDialog>

                <div class="grid grid-panel">
                    <Rock:ModalAlert ID="mdGridWarning" runat="server" />
                    <Rock:Grid ID="gList" runat="server" RowItemText="Journey Program"
                        OnRowSelected="gList_RowSelected" TooltipField="Description">
                        <Columns>
                            <Rock:ReorderField />
                            <Rock:RockBoundField DataField="Name" HeaderText="Name" />
                            <Rock:RockBoundField DataField="Description" HeaderText="Description"
                                TruncateLength="80" />
                            <Rock:RockBoundField DataField="SubGroupCount" HeaderText="Sub Groups"
                                ItemStyle-HorizontalAlign="Center" HeaderStyle-HorizontalAlign="Center" />
                            <Rock:DateTimeField DataField="LastRunDateTime" HeaderText="Last Run" />
                            <Rock:BoolField DataField="IsActive" HeaderText="Active" />
                            <Rock:SecurityField TitleField="Name" />
                            <Rock:DeleteField OnClick="gList_Delete" />
                        </Columns>
                    </Rock:Grid>
                </div>
            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
