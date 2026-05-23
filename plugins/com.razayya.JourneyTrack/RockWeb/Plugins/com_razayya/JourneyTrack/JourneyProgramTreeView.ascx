<%@ Control Language="C#" AutoEventWireup="true" CodeFile="JourneyProgramTreeView.ascx.cs"
    Inherits="RockWeb.Plugins.com_razayya.JourneyTrack.JourneyProgramTreeView" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <div class="treeview">
            <div class="panel panel-block">
                <div class="panel-heading">
                    <h1 class="panel-title">
                        <i class="fa fa-sitemap"></i>
                        Journey Programs
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
                                    <asp:LinkButton ID="lbAddCalculation" runat="server" Text="Add JourneyCalculation to Selected" OnClick="lbAddCalculation_Click" Enabled="false" />
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>
                <div class="panel-body" style="padding:0;">
                    <div class="treeview-scroll" style="max-height:calc(100vh - 280px);min-height:240px;overflow-y:auto;overflow-x:auto;padding:8px 12px;">
                        <div class="treeview-frame">
                            <asp:Literal ID="lTreeHtml" runat="server" />
                        </div>
                    </div>
                </div>
            </div>
        </div>

    </ContentTemplate>
</asp:UpdatePanel>

<style>
    .js-synctree-toggle { cursor: pointer; }
    .js-synctree-nav { margin-left: 4px; opacity: 0; transition: opacity 0.15s; font-size: 11px; }
    .rocktree-item:hover > .rocktree-name .js-synctree-nav,
    .rocktree-item:hover > .rocktree-icon + .rocktree-name .js-synctree-nav { opacity: 0.6; }
    .js-synctree-nav:hover { opacity: 1 !important; }
</style>

<script type="text/javascript">
    $(function () {
        $(document).off('click.synctree').on('click.synctree', '.js-synctree-toggle', function (e) {
            // Don't toggle when clicking the pencil nav link
            if ($(e.target).closest('.js-synctree-nav').length) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            var $item = $(this).closest('.rocktree-item');
            var $children = $item.children('.rocktree-children');
            var $chevron = $item.children('.rocktree-icon').find('i');

            if ($children.length === 0) {
                return;
            }

            if ($children.is(':visible')) {
                $children.slideUp(150);
                $chevron.removeClass('fa-chevron-down').addClass('fa-chevron-right');
            } else {
                $children.slideDown(150);
                $chevron.removeClass('fa-chevron-right').addClass('fa-chevron-down');
            }
        });
    });
</script>
