<%@ Control Language="C#" AutoEventWireup="true" CodeFile="PersonJourneyProgress.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.PersonJourneyProgress" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />
        <asp:Literal ID="lOutput" runat="server" />

        <%-- Empty-state per-program enroll prompts. Renders one card per
             configured program that the displayed person is NOT enrolled in,
             with a button to enroll them directly. --%>
        <asp:Repeater ID="rEnrollPrompts" runat="server" OnItemCommand="rEnrollPrompts_ItemCommand">
            <ItemTemplate>
                <div class="panel panel-block margin-b-md">
                    <div class="panel-body">
                        <div style="display:flex;justify-content:space-between;align-items:center;gap:1rem;">
                            <div>
                                <strong><%# Eval("ProgramName") %></strong>
                                <div class="text-muted small">This person is not currently enrolled.</div>
                            </div>
                            <asp:LinkButton runat="server" CssClass="btn btn-primary btn-sm"
                                CommandName="Enroll" CommandArgument='<%# Eval("ProgramId") %>'
                                CausesValidation="false">
                                <i class="fa fa-user-plus"></i> Enroll
                            </asp:LinkButton>
                        </div>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>

        <%-- Manual skip (7038): confirm + optional note. Opened by the drawer's
             Skip / Skip-remaining postback links; guarded by the ManageSkips
             block security action. --%>
        <Rock:ModalDialog ID="mdSkip" runat="server" Title="Skip Step" SaveButtonText="Skip" OnSaveClick="mdSkip_SaveClick" ValidationGroup="vgSkip">
            <Content>
                <asp:Literal ID="lSkipPrompt" runat="server" />
                <Rock:RockTextBox ID="tbSkipNote" runat="server" Label="Note (optional)" TextMode="MultiLine" Rows="2" ValidationGroup="vgSkip" />
            </Content>
        </Rock:ModalDialog>
    </ContentTemplate>
</asp:UpdatePanel>
