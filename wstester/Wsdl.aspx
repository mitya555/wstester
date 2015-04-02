<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Wsdl.aspx.cs" Inherits="wstester.Wsdl" %>
<%@ Import Namespace="System.Linq" %>
<%@ Import Namespace="System.Xml" %>
<%@ Import Namespace="System.Net" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="System.Xml.Schema" %>
<%@ Import Namespace="wstester" %>
<script runat="server">

	protected void Page_Load(object sender, EventArgs e)
	{
		if (!IsPostBack && Request["operation"] != null)
		{
			var operation = Wsdl.Operation.Deserialize(Request["operation"]);
			WsdlTextBox.Text = operation.wsdl;
			UsernameTextBox.Text = operation.username;
			PasswordTextBox.Text = operation.password;
			WsdlButton_Click(null, null);
			TreeView1.CollapseAll();
			var selected_node = TreeView1.FindNode(operation.TreeViewPath);
			if (selected_node != null)
			{
				var tmp_node = selected_node;
				while (tmp_node.Parent != null)
				{
					tmp_node.Parent.Expand();
					tmp_node = tmp_node.Parent;
				}
				selected_node.Parent.Expand();
				selected_node.Select();
			}
		}
		Form.DefaultButton = "WsdlButton";
	}

	protected void DelUrlsButton_Click(object sender, EventArgs e)
	{
		File.Delete(Server.MapPath("~/App_Data/WsdlList.json"));
	}

	protected void WsdlButton_Click(object sender, EventArgs e)
	{
		XmlDocument[] wsdl_;
		XmlNamespaceManager nsmgr;
		if (!WsdlTextBox.Text.Equals(Session["wsdl-url"]))
		{
			wsdl_ = LoadWsdl(WsdlTextBox.Text, out nsmgr, UsernameTextBox.Text, PasswordTextBox.Text);

			XmlSchemaSet xss = LoadCompileSchemas(wsdl_, nsmgr, UsernameTextBox.Text, PasswordTextBox.Text);

			AddWsdlUrlToJsonAutocompleteStorage(Server.MapPath("~/App_Data/WsdlList.json"), WsdlTextBox.Text);

			Session["wsdl-url"] = WsdlTextBox.Text;
			Session["wsdl"] = wsdl_;
			Session["nsmgr"] = nsmgr;
			Session["xss"] = xss;
		}
		else
		{
			wsdl_ = (XmlDocument[])Session["wsdl"];
			nsmgr = (XmlNamespaceManager)Session["nsmgr"];
		}

		BuildServiceTreeView(TreeView1, WsdlTextBox.Text, wsdl_, nsmgr, UsernameTextBox.Text, PasswordTextBox.Text);
	}

	protected void TreeView1_TreeNodeDataBound(object sender, TreeNodeEventArgs e)
	{

	}

	protected void TreeView1_TreeNodePopulate(object sender, TreeNodeEventArgs e)
	{

	}

	protected void TreeView1_DataBinding(object sender, EventArgs e)
	{

	}

	protected void TreeView1_DataBound(object sender, EventArgs e)
	{

	}
</script>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head id="Head1" runat="server">
	<title>WSDL/WADL</title>
	<script type="text/javascript" src="//ajax.googleapis.com/ajax/libs/jquery/1.8.3/jquery.min.js"></script>
	<script type="text/javascript" src="//ajax.googleapis.com/ajax/libs/jqueryui/1.9.2/jquery-ui.min.js"></script>
	<link type="text/css" rel="Stylesheet" href="//ajax.googleapis.com/ajax/libs/jqueryui/1.9.2/themes/redmond/jquery-ui.css" />
	<style type="text/css">
		body
		{
			font-family: Arial;
			font-size: 10pt;
		}
		.heading1 
		{
			color: #ffffff; 
			font-family: Tahoma; 
			font-size: 26px; 
			font-weight: normal; 
			background-color: #003366; 
			margin-top: 10px; 
			margin-bottom: 0px; 
			/*margin-left: -30px;*/
			padding-top: 10px; 
			padding-bottom: 3px; 
			padding-left: 15px; 
			/*width: 105%;*/
		}
		/*ul { margin-top: 10px; margin-left: 20px; }
		ol { margin-top: 10px; margin-left: 20px; }*/
		li { margin-top: 10px; color: #000000; }
		.link-button-class { white-space: nowrap; text-decoration: none; }
		.ui-autocomplete { font-size: 1em; }
	</style>
</head>
<body>
	<form id="form1" runat="server">
	<div>
		<table cellpadding="0" cellspacing="0" width="100%"><tr><td style="white-space:nowrap;">
			WSDL/WADL URL:&nbsp;&nbsp;
		</td><td style="width:100%;white-space:nowrap;">
			<asp:TextBox ID="WsdlTextBox" runat="server" Width="570px"></asp:TextBox>&nbsp;&nbsp;
			<asp:Button ID="WsdlButton" runat="server" onclick="WsdlButton_Click" CssClass="link-button-class" Text="Get WSDL/WADL" />&nbsp;&nbsp;
		</td></tr><tr style="font-size:8pt;font-style:italic;vertical-align:top;"><td>
			<asp:LinkButton ID="DelUrlsButton" runat="server" onclick="DelUrlsButton_Click" CssClass="link-button-class">Clear URL hints</asp:LinkButton>
		</td><td>
			<a href="javascript://" onclick="$('#basic-auth-id').toggle()" style="text-decoration:none;">Basic Authentcation</a><span id="basic-auth-id" style="display:none;">:<br /> 
			Username: <asp:TextBox ID="UsernameTextBox" runat ="server" Font-Size="8pt"></asp:TextBox> 
			Password: <asp:TextBox ID="PasswordTextBox" runat ="server" TextMode="Password" Font-Size="8pt"></asp:TextBox></span>
		</td></tr></table>
		<hr />
		<asp:TreeView ID="TreeView1" runat="server" ImageSet="Arrows" >
			<ParentNodeStyle Font-Bold="False" />
			<LevelStyles>
				<asp:TreeNodeStyle Font-Underline="False" ForeColor="Black" />
				<asp:TreeNodeStyle Font-Underline="False" ForeColor="Black" />
				<asp:TreeNodeStyle Font-Underline="False" />
				<asp:TreeNodeStyle Font-Underline="False" ForeColor="Black" CssClass="TreeViewDesc" />
			</LevelStyles>
			<%--<HoverNodeStyle Font-Underline="True" ForeColor="#5555DD" />--%>
			<SelectedNodeStyle Font-Bold="true"
				HorizontalPadding="0px" VerticalPadding="0px" /><%-- Font-Underline="True" ForeColor="#5555DD"--%>
			<NodeStyle Font-Names="Tahoma" Font-Size="10pt" 
				HorizontalPadding="5px" NodeSpacing="0px" VerticalPadding="0px" /><%-- ForeColor="Black"--%>
		</asp:TreeView>
		<%-- DataSourceID="XmlDataSource1" 
			AutoGenerateDataBindings="True" ondatabinding="TreeView1_DataBinding" 
			ondatabound="TreeView1_DataBound" 
			ontreenodedatabound="TreeView1_TreeNodeDataBound" 
			ontreenodepopulate="TreeView1_TreeNodePopulate" EnableViewState="False">
			<DataBindings>
				<asp:TreeNodeBinding DataMember="wsdl:definitions" TextField="targetNamespace" />
				<asp:TreeNodeBinding DataMember="wsdl:service" TextField="name" />
				<asp:TreeNodeBinding DataMember="wsdl:port" TextField="name" />
			</DataBindings>
		</asp:TreeView>
		<asp:XmlDataSource ID="XmlDataSource1" runat="server"><data><root /></data></asp:XmlDataSource>--%>
		
		<div id="result" runat="server"></div>
		<pre id="pre_result" runat="server"></pre>
	</div>
	</form>
</body>
<script type="text/javascript">
	(function ($) {
		$('#<% = TreeView1.ClientID %> a').has('img').attr('hideFocus', 'hidefocus');
		$('#<% = TreeView1.ClientID %> a:first').focus();
		$('a.TreeViewDesc').css({ 'white-space': 'normal', 'font-style': 'italic', 'font-size': '8pt' })
			.wrap('<div style="width: 400px;"></' + 'div>');
		var _$wsdl_inp_ = $('#<% = WsdlTextBox.ClientID %>'), _wsdl_val_;
		_$wsdl_inp_.autocomplete({
			source: 'Urls.ashx',
			select: function (event, ui) {
				_$wsdl_inp_.val(ui.item.value);
				if (_wsdl_val_ !== ui.item.value)
					<%= ClientScript.GetPostBackEventReference(WsdlButton, "", false) %>;
			}
		}).focus(function () {
			_wsdl_val_ = this.value;
			this.select();
		});
	})(jQuery);
</script>
</html>
