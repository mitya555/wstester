<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Elem.aspx.cs" Inherits="wstester.Elem" 
	MaintainScrollPositionOnPostback="true" ValidateRequest="false" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Net" %>
<%@ Import Namespace="System.Net.Sockets" %>
<%@ Import Namespace="System.Xml" %>
<%@ Import Namespace="System.Xml.Schema" %>
<%@ Import Namespace="System.Linq" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="wstester" %>
<script runat="server">

	protected void Page_PreInit(object sender, EventArgs e)
	{
		if (IsPostBack || Request["element"].Equals(Session["element"]))
		{
			schemaNodes = (SchemaNodes)Session["xsd-SchemaNodes"];
			//using (var stream = new MemoryStream(Convert.FromBase64String(Request.Form["SchemaNodes"]), false))
			//using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress))
			//	schemaNodes = (SchemaNodes)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(stream);
		}
		else
		{
			var xsd_ = (IDictionary<string, XmlDocument>)Session["xsd-xsd"];
			var nsmgr = (XmlNamespaceManager)Session["xsd-nsmgr"];
			var xss = (XmlSchemaSet)Session["xsd-xss"];

			var element = Xsd.Element.Deserialize(Request["element"]);

			var schema = xss.Schemas(element.elem.Namespace).Cast<XmlSchema>().First();

			schemaNodes = SchemaNodes.FromXmlSchema(schema.Elements[element.elem]);

			Session["xsd-element"] = Request["element"];
			Session["xsd-SchemaNodes"] = schemaNodes;
		}
	}
	
	protected void Page_Init(object sender, EventArgs e)
	{
		AddButtonControl(CtrlContainer, new Button() { ID = "preview-button", CssClass = "submit-button-class" }, "Preview", btnXml_Click);
	}

	//protected void Page_PreRender(object sender, EventArgs e)
	//{
	//	using (var stream = new MemoryStream())
	//	using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.Default))
	//	{
	//		new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(stream, schemaNodes);
	//		ClientScript.RegisterHiddenField("SchemaNodes", Convert.ToBase64String(stream.ToArray()));
	//	}
	//}

	void btnXml_Click(object sender, EventArgs e)
	{
		XmlDocument doc = new XmlDocument();
		doc.AppendChild(doc.CreateXmlDeclaration("1.0", null, null));
		//return doc
		//	.AppendChild(doc.CreateElement("Envelope", envelopeNamespace))
		//	.AppendChild(doc.CreateElement("Body", envelopeNamespace));
		BuildXml(schemaNodes.Nodes, doc);
		var xml = doc.GetXml(true);

		AddVerticalPadding(Page.Form);
		AddLiteral(Page.Form, "<div id='xml-div'><pre>" + Server.HtmlEncode(xml) + "</pre></div>");

		ClientScript.RegisterStartupScript(GetType(), "xml-fancybox-startup", @"
	jQuery(function($) {
		$.fancybox.open($.extend({ type: 'inline', href: '#xml-div' }, get_fancybox_options('80%', '80%')));
	});
", true);
	}
</script>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head id="Head1" runat="server">
	<title>Edit XML</title>
	<style type="text/css">
		body, input, select { font: 8pt Arial; }
		.input-image-class, .input-text-class { height: 9pt; /*border-style: none;*/ vertical-align: middle; }
		.input-text-class { background-color: #dcecfc; width: 250px; border: solid #dcecfc; }
		.input-select-class { height: 10pt; /*margin-top: -1px;*/ }
		.panel-class { padding: 0px 3px 0px 12px; }
		.vertical-margin { height: 3px; }
		.title-class { background-color: #B8BFD8; color: #AA0000; font-weight: bold; /*font-family: Verdana;*/ }
		.title-class a { color: white; font-weight: bold; font-family: Verdana; font-size: 7.5pt; vertical-align: top; }
		.hr-class { display: none; }
		a, a:hover { text-decoration: none; }
		.submit-button-class { width: 75px; margin-right: 5px; }
		.placeholder { color: #aaa; }
	</style>
	<script type="text/javascript" src="//ajax.googleapis.com/ajax/libs/jquery/1.8.2/jquery.min.js"></script>
	<!-- Add fancyBox -->
	<%--<link type='text/css' rel='Stylesheet' href='~/js/fancybox213/jquery.fancybox.css?v=2.1.3' media='screen' />--%>
	<style type="text/css">
		@import url('<%= ResolveUrl("~/js/fancybox213/jquery.fancybox.css?v=2.1.3") %>')
	</style>
	<script type='text/javascript' src='<%= ResolveUrl("~/js/fancybox213/jquery.fancybox.pack.js?v=2.1.3") %>'></script>
	<script type='text/javascript' src='<%= ResolveUrl("~/js/app-fancybox.js") %>'></script>
	<script type='text/javascript' src='<%= ResolveUrl("~/js/jquery.placeholder.min.js") %>'></script>
</head>
<body>
	<a href='Xsd.aspx?element=<%= Server.UrlEncode(Request["element"]) %>'>&laquo; back to XSD</a>
	<form id="form1" runat="server">
		<!--<table border="0" width="100%"><tr>
			<td nowrap>Schema URL:&nbsp;</td>
			<td width="100%"><asp:TextBox ID="txSchemaUrl" runat="server" Width="100%"></asp:TextBox></td>
		</tr></table>-->
	</form>
</body>
</html>
