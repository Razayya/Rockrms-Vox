<%@ Control Language="C#" AutoEventWireup="true" CodeFile="CalculationGroupTreeView.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.CustomPersonAttributeSyncEngine.CalculationGroupTreeView" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="treeview">
            <div class="panel panel-block">
                <div class="panel-heading">
                    <h1 class="panel-title">
                        <i class="fa fa-sitemap"></i>
                        Calculation Groups
                    </h1>
                    <div class="panel-labels treeview-actions">
                        <div class="btn-group">
                            <button type="button" class="btn btn-link btn-xs dropdown-toggle" data-toggle="dropdown" title="Add Item">
                                <i class="fa fa-plus"></i>
                            </button>
                            <ul class="dropdown-menu dropdown-menu-right" role="menu">
                                <li>
                                    <asp:LinkButton ID="lbAddGroup" runat="server" Text="Add Group" OnClick="lbAddGroup_Click" />
                                </li>
                                <li>
                                    <asp:LinkButton ID="lbAddSubGroup" runat="server" Text="Add Sub Group to Selected" OnClick="lbAddSubGroup_Click" Enabled="false" />
                                </li>
                                <li>
                                    <asp:LinkButton ID="lbAddCalculation" runat="server" Text="Add Calculation to Selected" OnClick="lbAddCalculation_Click" Enabled="false" />
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>
                <div class="panel-body">
                    <div class="treeview-scroll scroll-container scroll-container-horizontal">
                        <div class="viewport">
                            <div class="overview">
                                <div class="treeview-frame">
                                    <asp:Literal ID="lTreeHtml" runat="server" />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <script type="text/javascript">
            Sys.Application.add_load(function () {
                $('.js-synctree-toggle').off('click.synctree').on('click.synctree', function (e) {
                    e.preventDefault();
                    e.stopPropagation();

                    var $item = $(this).closest('.rocktree-item');
                    var $children = $item.children('.rocktree-children');
                    var $icon = $(this).find('i');

                    if ($children.is(':visible')) {
                        $children.slideUp(150);
                        $icon.removeClass('fa-chevron-down').addClass('fa-chevron-right');
                    } else {
                        $children.slideDown(150);
                        $icon.removeClass('fa-chevron-right').addClass('fa-chevron-down');
                    }
                });
            });
        </script>
    </ContentTemplate>
</asp:UpdatePanel>
