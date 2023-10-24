<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ProjectProjectBoardList.ascx.cs" Inherits="RockWeb.Plugins.Razayya.ProjectManagement.ProjectProjectBoardList" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:HiddenField ID="hfOptionsVisible" runat="server" Value="0" />

        <asp:Panel ID="pnlProjectList" runat="server" CssClass="panel panel-block" Visible="false">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <asp:Literal ID="ltHtmlIcon" runat="server" />
                    <span>Sub Projects</span>
                </h1>

                <div class="panel-labels">
                    <asp:LinkButton ID="lbAdd" runat="server" CssClass="btn btn-xs pm-btn-xs btn-link" OnClick="lbAdd_Click"><i class="fa fa-plus"></i></asp:LinkButton>
                    <a class="btn btn-xs pm-btn-xs btn-link js-subproject-btn-options"><i class="fa fa-gear pm-fa-rotate"></i></a>
                </div>
            </div>

            <asp:Panel ID="pnlOptions" runat="server" CssClass="pm-second-heading collapse js-subproject-options">
                <asp:CheckBox ID="cbShowInactive" runat="server" Text="Show Inactive" OnCheckedChanged="cbShowInactive_CheckedChanged" AutoPostBack="true" CausesValidation="false" />
            </asp:Panel>

            <ul class="list-group pm-list-group">
                <asp:Repeater ID="rpProject" runat="server" OnItemDataBound="rpProject_ItemDataBound" OnItemCommand="rpProject_ItemCommand">
                    <ItemTemplate>
                        <li id="liProject" runat="server" class="list-group-item pm-group-item" data-toggle="tooltip" data-delay="600" data-placement="auto left">
                            <div class="pm-group-item-heading clearfix">
                                <asp:LinkButton ID="lbProjectName" runat="server" CssClass="pm-group-item-name" CommandName="ShowProject" CommandArgument='<%# Eval( "Id" ) %>'></asp:LinkButton>
                            </div>

                            <div class="pm-group-item-details">
                                <asp:Literal ID="lDueDate" runat="server"></asp:Literal>
                                <asp:Literal ID="lBlockedBy" runat="server"></asp:Literal>
                                <asp:Literal ID="lDescription" runat="server"></asp:Literal>
                                <asp:Literal ID="lAssignedTo" runat="server"></asp:Literal>
                            </div>
                        </li>
                    </ItemTemplate>
                </asp:Repeater>
            </ul>
        </asp:Panel>

        <script>
            (function ($)
            {
                Sys.Application.add_load(function ()
                {
                    $('[data-toggle="tooltip"]').tooltip();

                    $('.js-subproject-btn-options').on('click', function () {
                        var $target = $('.js-subproject-options')
                        var makeVisible = !$target.is(':visible');

                        $target.slideToggle(250);
                        $(this).find('.fa').toggleClass('down');

                        $('#<%= hfOptionsVisible.ClientID %>').val(makeVisible ? '1' : '0');
                    });
                })
            })(jQuery);
        </script>
    </ContentTemplate>
</asp:UpdatePanel>