﻿// <copyright>
// Copyright by BEMA Information Technologies
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Rock.Plugin;

namespace com.bemaservices.ClientPackage.BEMA
{
    [MigrationNumber( 13, "1.9.4" )]
    public class BaptismOverTimeChart : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Page: Baptisms Over Time Chart
            RockMigrationHelper.AddPage( "2571CBBD-7CCA-4B24-AAAB-107FD136298B", "D65F783D-87A9-4CC9-8110-E83466A0EADB", "Baptisms Over Time Chart", "Bar Chart of Baptism Dates over Time", "1EB4A10C-DE48-4182-97E6-37B23EFFA211", "fa fa-tint" ); // Site:Rock RMS
            RockMigrationHelper.UpdateBlockType( "HTML Content", "Adds an editable HTML fragment to the page.", "~/Blocks/Cms/HtmlContentDetail.ascx", "CMS", "19B61D65-37E3-459F-A44F-DEF0089118A3" );
            RockMigrationHelper.UpdateBlockType( "Dynamic Chart", "Block to display a chart using SQL as the chart datasource", "~/Blocks/Reporting/DynamicChart.ascx", "Reporting", "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723" );
            // Add Block to Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlock( true, "1EB4A10C-DE48-4182-97E6-37B23EFFA211", "", "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "Baptisms", "Main", "", "", 0, "C19F1155-93D4-4217-96C3-90D752066434" );
            // Add Block to Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlock( true, "1EB4A10C-DE48-4182-97E6-37B23EFFA211", "", "19B61D65-37E3-459F-A44F-DEF0089118A3", "HTML Content", "Feature", "", "", 0, "094246C7-2DBC-4F9B-A791-BFAEF29E998F" );
            // Attrib for BlockType: HTML Content:Enabled Lava Commands
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "4BD9088F-5CC6-89B1-45FC-A2AAFFC7CC0D", "Enabled Lava Commands", "EnabledLavaCommands", "", "The Lava commands that should be enabled for this HTML block.", 0, @"", "7146AC24-9250-4FC4-9DF2-9803B9A84299" );
            // Attrib for BlockType: HTML Content:Entity Type
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "3549BAB6-FE1B-4333-AFC4-C5ACA01BB8EB", "Entity Type", "ContextEntityType", "", "The type of entity that will provide context for this block", 0, @"", "6783D47D-92F9-4F48-93C0-16111D675A0F" );
            // Attrib for BlockType: Dynamic Chart:Query Params
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Query Params", "QueryParams", "", "The parameters that the stored procedure expects in the format of 'param1=value;param2=value'. Any parameter with the same name as a page parameter (i.e. querystring, form, or page route) will have its value replaced with the page's current value. A parameter with the name of 'CurrentPersonId' will have its value replaced with the currently logged in person's id.", 0, @"", "0D7A45A6-C885-44CD-9FA9-B8F431D943B5" );
            // Attrib for BlockType: Dynamic Chart:Title
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Title", "Title", "", "The title of the widget", 0, @"", "94FCBF87-B633-4F02-887F-A99970129319" );
            // Attrib for BlockType: Dynamic Chart:Chart Height
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "A75DFC58-7A1B-4799-BF31-451B2BBE38FF", "Chart Height", "ChartHeight", "", "", 0, @"200", "BFAF6EB4-5181-4473-A222-3C2F12982956" );
            // Attrib for BlockType: Dynamic Chart:SQL
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "SQL", "SQL", "", "The SQL for the datasource. Output columns must be as follows:  <ul>      <li>Bar or Line Chart          <ul>             <li>[SeriesName] : string or numeric </li>             <li>[DateTime] : DateTime </li>             <li>[YValue] : numeric </li>          </ul>      </li>      <li>Pie Chart          <ul>             <li>[MetricTitle] : string </li>             <li>[YValueTotal] : numeric </li>          </ul>      </li>  </ul>    Example:   <code><pre>  -- get top 25 viewed pages from the last 30 days (excluding Home)  select top 25  * from (      select           distinct          pv.PageTitle [SeriesName],           convert(date, pv.DateTimeViewed) [DateTime],           count(*) [YValue]       from           PageView pv      where PageTitle is not null          group by pv.PageTitle, convert(date, pv.DateTimeViewed)      ) x where SeriesID != 'Home'   and DateTime > DateAdd(day, -30, SysDateTime())  order by YValue desc  </pre>  </code>", 0, @"", "C95DFED0-D082-4D47-A4C0-ADD8664A9CA8" );
            // Attrib for BlockType: Dynamic Chart:Subtitle
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Subtitle", "Subtitle", "", "The subtitle of the widget", 1, @"", "3006CAEC-2DBD-4C87-BCA7-5AB23657497E" );
            // Attrib for BlockType: HTML Content:Start in Code Editor mode
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Start in Code Editor mode", "UseCodeEditor", "", "Start the editor in code editor mode instead of WYSIWYG editor mode.", 1, @"True", "0673E015-F8DD-4A52-B380-C758011331B2" );
            // Attrib for BlockType: HTML Content:Document Root Folder
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Document Root Folder", "DocumentRootFolder", "", "The folder to use as the root when browsing or uploading documents.", 2, @"~/Content", "3BDB8AED-32C5-4879-B1CB-8FC7C8336534" );
            // Attrib for BlockType: Dynamic Chart:Column Width
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "7525C4CB-EE6B-41D4-9B64-A08048D5A5C0", "Column Width", "ColumnWidth", "", "The width of the widget.", 2, @"4", "9B4B5CBF-1E80-4CCA-8B6D-806806038188" );
            // Attrib for BlockType: Dynamic Chart:Chart Style
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "59D5A94C-94A0-4630-B80A-BB25697D74C7", "Chart Style", "ChartStyle", "", "", 3, @"", "BAAE0B29-7D8C-4369-93F2-E1B624882170" );
            // Attrib for BlockType: HTML Content:Image Root Folder
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Image Root Folder", "ImageRootFolder", "", "The folder to use as the root when browsing or uploading images.", 3, @"~/Content", "26F3AFC6-C05B-44A4-8593-AFE1D9969B0E" );
            // Attrib for BlockType: HTML Content:User Specific Folders
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "User Specific Folders", "UserSpecificFolders", "", "Should the root folders be specific to current user?", 4, @"False", "9D3E4ED9-1BEF-4547-B6B0-CE29FE3835EE" );
            // Attrib for BlockType: HTML Content:Cache Duration
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "A75DFC58-7A1B-4799-BF31-451B2BBE38FF", "Cache Duration", "CacheDuration", "", "Number of seconds to cache the content.", 5, @"0", "4DFDB295-6D0F-40A1-BEF9-7B70C56F66C4" );
            // Attrib for BlockType: HTML Content:Context Parameter
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Context Parameter", "ContextParameter", "", "Query string parameter to use for 'personalizing' content based on unique values.", 6, @"", "3FFC512D-A576-4289-B648-905FD7A64ABB" );
            // Attrib for BlockType: HTML Content:Context Name
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "9C204CD0-1233-41C5-818A-C5DA439445AA", "Context Name", "ContextName", "", "Name to use to further 'personalize' content.  Blocks with the same name, and referenced with the same context parameter will share html values.", 7, @"", "466993F7-D838-447A-97E7-8BBDA6A57289" );
            // Attrib for BlockType: Dynamic Chart:Show Legend
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Show Legend", "ShowLegend", "", "", 7, @"True", "55D7DEB8-1D23-430B-81EF-D7F42FFAD518" );
            // Attrib for BlockType: HTML Content:Enable Versioning
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Enable Versioning", "SupportVersions", "", "If checked, previous versions of the content will be preserved. Versioning is required if you want to require approval.", 8, @"False", "7C1CE199-86CF-4EAE-8AB3-848416A72C58" );
            // Attrib for BlockType: Dynamic Chart:Legend Position
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "7525C4CB-EE6B-41D4-9B64-A08048D5A5C0", "Legend Position", "LegendPosition", "", "Select the position of the Legend (corner)", 8, @"ne", "398336FF-64CD-4EB8-A78C-9C9DAEBF68FA" );
            // Attrib for BlockType: Dynamic Chart:Chart Type
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "7525C4CB-EE6B-41D4-9B64-A08048D5A5C0", "Chart Type", "ChartType", "", "", 9, @"Line", "F5626814-8713-420D-A93C-CFE6E89321DF" );
            // Attrib for BlockType: HTML Content:Require Approval
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Require Approval", "RequireApproval", "", "Require that content be approved?", 9, @"False", "EC2B701B-4C1D-4F3F-9C77-A73C75D7FF7A" );
            // Attrib for BlockType: HTML Content:Cache Tags
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "BD0D9B57-2A41-4490-89FF-F01DAB7D4904", "Cache Tags", "CacheTags", "", "Cached tags are used to link cached content so that it can be expired as a group", 10, @"", "522C18A9-C727-42A5-A0BA-13C673E8C4B6" );
            // Attrib for BlockType: Dynamic Chart:Pie Inner Radius
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "C757A554-3009-4214-B05D-CEA2B2EA6B8F", "Pie Inner Radius", "PieInnerRadius", "", "If this is a pie chart, specific the inner radius to have a donut hole. For example, specify: 0.75 to have the inner radius as 75% of the outer radius.", 10, @"0", "4CAF4C44-586D-490E-918D-52C0BD81BB3C" );
            // Attrib for BlockType: Dynamic Chart:Pie Show Labels
            RockMigrationHelper.UpdateBlockTypeAttribute( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Pie Show Labels", "PieShowLabels", "", "If this is a pie chart, specify if labels show be shown", 11, @"True", "3B54049D-60A4-4473-BA10-49101E06F150" );
            // Attrib for BlockType: HTML Content:Is Secondary Block
            RockMigrationHelper.UpdateBlockTypeAttribute( "19B61D65-37E3-459F-A44F-DEF0089118A3", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Is Secondary Block", "IsSecondaryBlock", "", "Flag indicating whether this block is considered secondary and should be hidden when other secondary blocks are hidden.", 11, @"False", "04C15DC1-DFB6-4D63-A7BC-0507D0E33EF4" );
            // Attrib Value for Block:Baptisms, Attribute:Title Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "94FCBF87-B633-4F02-887F-A99970129319", @"Baptisms Over Time" );
            // Attrib Value for Block:Baptisms, Attribute:Legend Position Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "398336FF-64CD-4EB8-A78C-9C9DAEBF68FA", @"ne" );
            // Attrib Value for Block:Baptisms, Attribute:Chart Type Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "F5626814-8713-420D-A93C-CFE6E89321DF", @"Bar" );
            // Attrib Value for Block:Baptisms, Attribute:Column Width Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "9B4B5CBF-1E80-4CCA-8B6D-806806038188", @"12" );
            // Attrib Value for Block:Baptisms, Attribute:Chart Height Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "BFAF6EB4-5181-4473-A222-3C2F12982956", @"300" );
            // Attrib Value for Block:Baptisms, Attribute:Show Legend Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "55D7DEB8-1D23-430B-81EF-D7F42FFAD518", @"True" );
            // Attrib Value for Block:Baptisms, Attribute:Pie Show Labels Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "3B54049D-60A4-4473-BA10-49101E06F150", @"True" );
            // Attrib Value for Block:Baptisms, Attribute:SQL Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "C95DFED0-D082-4D47-A4C0-ADD8664A9CA8", @" {% include '~/Plugins/com_bemaservices/CustomBlocks/BEMA/Assets/Sql/BaptismOverTimeChart.sql' %}" );
            // Attrib Value for Block:Baptisms, Attribute:Chart Style Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "BAAE0B29-7D8C-4369-93F2-E1B624882170", @"2abb2ea0-b551-476c-8f6b-478cd08c2227" );
            // Attrib Value for Block:Baptisms, Attribute:Pie Inner Radius Page: Baptisms Over Time Chart, Site: Rock RMS
            RockMigrationHelper.AddBlockAttributeValue( "C19F1155-93D4-4217-96C3-90D752066434", "4CAF4C44-586D-490E-918D-52C0BD81BB3C", @"0" );

            RockMigrationHelper.AddSecurityAuthForPage( "1EB4A10C-DE48-4182-97E6-37B23EFFA211", 0, "View", false, "", 1, "77E4D0CC-3373-480F-BAA5-E6557D6132EB" );
            RockMigrationHelper.UpdateHtmlContentBlock( "094246C7-2DBC-4F9B-A791-BFAEF29E998F", @"<div class='alert alert-info'>
    Bar Chart of Baptism Dates over Time
</div>"
           , "BB93C927-5A23-4108-850B-522284A9618A" );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( "4CAF4C44-586D-490E-918D-52C0BD81BB3C" );
            RockMigrationHelper.DeleteAttribute( "BAAE0B29-7D8C-4369-93F2-E1B624882170" );
            RockMigrationHelper.DeleteAttribute( "C95DFED0-D082-4D47-A4C0-ADD8664A9CA8" );
            RockMigrationHelper.DeleteAttribute( "3B54049D-60A4-4473-BA10-49101E06F150" );
            RockMigrationHelper.DeleteAttribute( "55D7DEB8-1D23-430B-81EF-D7F42FFAD518" );
            RockMigrationHelper.DeleteAttribute( "BFAF6EB4-5181-4473-A222-3C2F12982956" );
            RockMigrationHelper.DeleteAttribute( "9B4B5CBF-1E80-4CCA-8B6D-806806038188" );
            RockMigrationHelper.DeleteAttribute( "F5626814-8713-420D-A93C-CFE6E89321DF" );
            RockMigrationHelper.DeleteAttribute( "398336FF-64CD-4EB8-A78C-9C9DAEBF68FA" );
            RockMigrationHelper.DeleteAttribute( "3006CAEC-2DBD-4C87-BCA7-5AB23657497E" );
            RockMigrationHelper.DeleteAttribute( "94FCBF87-B633-4F02-887F-A99970129319" );
            RockMigrationHelper.DeleteAttribute( "0D7A45A6-C885-44CD-9FA9-B8F431D943B5" );
            RockMigrationHelper.DeleteBlock( "C19F1155-93D4-4217-96C3-90D752066434" );
            RockMigrationHelper.DeleteBlockType( "7BCCBFB0-26A5-4376-B1F3-DC6ADD7C3723" );
            RockMigrationHelper.DeletePage( "1EB4A10C-DE48-4182-97E6-37B23EFFA211" ); // Page: Baptisms Over Time Chart
        }
    }
}

