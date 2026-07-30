<%@ Control Language="C#" AutoEventWireup="true" CodeFile="GroupAttendancePicker.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.Controls.GroupAttendancePicker" %>

<asp:Panel ID="pnlEditor" runat="server" CssClass="jt-group-attendance-editor">
    <div class="row">
        <div class="col-md-6">
            <Rock:GroupTypesPicker ID="gtpGroupTypes" runat="server" Label="Group Types"
                AutoPostBack="true" OnSelectedIndexChanged="gtpGroupTypes_SelectedIndexChanged"
                Help="Pick the group type(s) whose groups you want to check attendance for. This filters the Groups list at right." />
        </div>
        <div class="col-md-6">
            <Rock:GroupPicker ID="gpGroups" runat="server" Label="Groups" AllowMultiSelect="true"
                Help="Pick the specific group(s) to check attendance for. The list is limited to the group type(s) selected at left." />
        </div>
    </div>
</asp:Panel>
