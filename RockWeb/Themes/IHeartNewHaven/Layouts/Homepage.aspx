<%@ Page Language="C#" MasterPageFile="Site.Master" AutoEventWireup="true" Inherits="Rock.Web.UI.RockPage" %>

<asp:Content ID="ctFeature" ContentPlaceHolderID="feature" runat="server">

    <div class="hero banner_home">
        <div class="hero_text">
            <Rock:Zone Name="Feature" runat="server" />
        </div>
        <!-- <video class="video_bg" id="uploadplayer-player-1711510505057_html5_api" poster="../assets/i-heart-new-haven---bridges-of-hope-mp4_543.jpg" muted loop="" preload="none" autoplay="" src="../assets/i-heart-new-haven---bridges-of-hope-mp4_543.mp4">
            <source src="../assets/i-heart-new-haven---bridges-of-hope-mp4_543.mp4" type="video/mp4">
        </video> -->

    </div>

</asp:Content>

<asp:Content ID="ctMain" ContentPlaceHolderID="main" runat="server">

	<section class="intro padding">
        <div class="container">

            <!-- Start Content Area -->

            <!-- Ajax Error -->
            <div class="alert alert-danger ajax-error no-index" style="display:none">
                <p><strong>Error</strong></p>
                <span class="ajax-error-message"></span>
            </div>

            <div class="intro_wrapper">
                <Rock:Zone Name="Sub Feature" runat="server" />
            </div>
        </div>
    </section>

    <section class="opportunities padding">
        <div class="container">

            <div class="row">
                <div class="col-md-12">
                    <Rock:Zone Name="Section A" runat="server" />
                </div>
            </div>

            <div class="row">
                <div class="col-md-4">
                    <Rock:Zone Name="Section B" runat="server" />
                </div>
                <div class="col-md-4">
                    <Rock:Zone Name="Section C" runat="server" />
                </div>
                <div class="col-md-4">
                    <Rock:Zone Name="Section D" runat="server" />
                </div>
            </div>

        </div>
    </section>

</asp:Content>

