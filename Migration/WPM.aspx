<%@ Page Language="C#" AutoEventWireup="True" CodeFile="WPM.aspx.cs" Inherits="SitefinityWebApp.Migrate.WPM" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <title></title>

    <style>
        #wrapper { width: 980px; margin: 0 auto; }
    </style>
</head>
<body>
    <form id="form1" class="form-horizontal" runat="server">
        <div id="wrapper">
            <div class="control-group">
                <label class="control-label" for="WordPressFile">WordPress Export File</label>
                <div class="controls">
                    <asp:FileUpload ID="WordPressFile" runat="server" />
                </div>
            </div>

            <div class="control-group">
                <label class="control-label" for="TargetBlog">Parent Blog</label>
                <div class="controls">
                    <asp:DropDownList ID="TargetBlog" ClientIDMode="Static" runat="server">
                        <asp:ListItem Value="">No blogs found!</asp:ListItem>
                    </asp:DropDownList>
                    <span class="help-block">Which blog would you like to import to?</span>
                </div>
            </div>

            <div class="control-group">
                <label class="control-label" for="ddlTagsImportMode">Post Tagging</label>
                <div class="controls">
                    <asp:DropDownList ID="ddlTagsImportMode" runat="server">
                        <asp:ListItem Text="Don't import tags" Value="0" />
                        <asp:ListItem Text="Import but don't create new ones" Value="1" />
                        <asp:ListItem Text="Import and create tags that are missing" Value="2" />
                    </asp:DropDownList>
                    <span class="help-block">Would you like to handle post tags?</span>
                </div>
            </div>

            <div class="control-group">
                <label class="control-label" for="ddlCategoriesImportMode">Post Categorization</label>
                <div class="controls">
                    <asp:DropDownList ID="ddlCategoriesImportMode" runat="server">
                        <asp:ListItem Text="Don't import categories" Value="0" />
                        <asp:ListItem Text="Import but don't create new ones" Value="1" />
                        <asp:ListItem Text="Import and create categories that are missing" Value="2" />
                    </asp:DropDownList>
                    <span class="help-block">Would you like to handle post categories?</span>
                </div>
            </div>

            <div class="control-group">
                <label class="control-label" for="chkImportComments">Import Comments</label>
                <div class="controls">
                    <asp:CheckBox ID="chkImportComments" runat="server" />
                    <span class="help-block">Would you like to import comments?</span>
                </div>
            </div>

            <div class="control-group">
                <label class="control-label" for="CurrentDomain">Current Domain</label>
                <div class="controls">
                    <asp:TextBox ID="CurrentDomain" ClientIDMode="Static" runat="server" />
                    <span class="help-block">http://www.currentdomain.com (Used to 'fix' image relative image URIs)</span>
                </div>
            </div>

            <asp:Button ID="Submit" runat="server" Text="Run" OnClick="Submit_Click" />
        </div>
    </form>
</body>
</html>
