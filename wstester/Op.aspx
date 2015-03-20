<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Op.aspx.cs" Inherits="wstester.Op" 
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

	private HiddenField bodyChanged;
	private CheckBox formatResultXml, showResultInPopup;

	protected void Page_PreInit(object sender, EventArgs e)
	{
		if (IsPostBack || Request["operation"].Equals(Session["Operation"]))
		{
			schemaNodes = (SchemaNodes)Session["SchemaNodes"];
			//using (var stream = new MemoryStream(Convert.FromBase64String(Request.Form["SchemaNodes"]), false))
			//using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress))
			//	schemaNodes = (SchemaNodes)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(stream);
		}
		else
		{
			var wsdl_ = (XmlDocument[])Session["wsdl"];
			var nsmgr = (XmlNamespaceManager)Session["nsmgr"];
			var xss = (XmlSchemaSet)Session["xss"];

			var operation = Wsdl.Operation.Deserialize(Request["operation"]);

			var msg_in = wsdl_.SelectSingleNodeByNameGlobal("/w:definitions/w:message", operation.msg_in, nsmgr)
				.SelectSingleNode("w:part"/*"[@name='parameters']"*/, nsmgr).AttrValueAsName("element");
			var schema = xss.Schemas(msg_in.Namespace).Cast<XmlSchema>().First();

			schemaNodes = SchemaNodes.FromXmlSchema(schema.Elements[msg_in]);

			var service = wsdl_.GetServiceByName(operation.service, nsmgr);
			var port = service.GetPortFromServiceByName(operation.port, nsmgr);
			var binding = port.GetBindingFromPort(wsdl_, nsmgr);
			var typed_binding = binding.ChildNodes.OfType<XmlElement>().FirstOrDefault(xml => "binding".Equals(xml.LocalName));
			var bindingNamespacePrefix = typed_binding != null ? nsmgr.LookupPrefix(typed_binding.NamespaceURI) : "http";
			var portType = binding.GetPortTypeFromBinding(wsdl_, nsmgr);
			var portType_op = portType.GetOperationFromParentByName(operation.op, nsmgr);

			var port_address = port.ChildNodes.OfType<XmlElement>().FirstOrDefault(xml => "address".Equals(xml.LocalName));
			var port_url = new Uri(port_address != null ? port_address.Attributes["location"].Value : operation.wsdl.Split('?')[0]);
			if (port_address != null && ("localhost".Equals(port_url.Host) || "127.0.0.1".Equals(port_url.Host)))
			{
				var uri_builder = new UriBuilder(port_url);
				uri_builder.Host = new Uri(operation.wsdl).Host;
				port_url = uri_builder.Uri;
			}

			var binding_op = binding.GetOperationFromParentByName(operation.op, nsmgr);
			var typed_op = binding_op.SelectSingleNode(bindingNamespacePrefix + ":operation", nsmgr);

			Headers.Dict.Clear();
			Headers.Insert(new Headers.HttpHeader() { Name = "Host", Value = port_url.Authority });
			switch (bindingNamespacePrefix)
			{
				case "soap":
					Headers.Insert(new Headers.HttpHeader() { Name = "Content-Length" });
					Headers.Insert(new Headers.HttpHeader() { Name = "Content-Type", Value = "text/xml; charset=utf-8" });
					Headers.Insert(new Headers.HttpHeader() { Name = "SOAPAction", Value = "\"" + typed_op.Attributes["soapAction"].Value + "\"" });
					break;
				case "soap12":
					Headers.Insert(new Headers.HttpHeader() { Name = "Content-Length" });
					Headers.Insert(new Headers.HttpHeader() { Name = "Content-Type", Value = "application/soap+xml; charset=utf-8" });
					break;
				case "http":
					if ("POST".Equals(typed_binding != null ? typed_binding.Attributes["verb"].Value : "POST"))
					{
						Headers.Insert(new Headers.HttpHeader() { Name = "Content-Length" });
						Headers.Insert(new Headers.HttpHeader() { Name = "Content-Type", Value = "application/x-www-form-urlencoded" });
					}
					break;
			}
			if ("" + operation.username != "")
				Headers.Insert(new Headers.HttpHeader()
				{
					Name = "Authorization",
					Value = "Basic " + Convert.ToBase64String(new UTF8Encoding().GetBytes(operation.username + ":" + operation.password))
				});

			Session["binding-namespace-prefix"] = bindingNamespacePrefix;
			Session["port-url"] = port_url;
			Session["Operation"] = Request["operation"];
			Session["SchemaNodes"] = schemaNodes;
		}
	}

	protected void Page_Init(object sender, EventArgs e)
	{
//        var headers = (Button)AddButtonControl(CtrlContainer, new Button(), "Headers", null);
//        headers.UseSubmitBehavior = false;
//        headers.OnClientClick = @"javascript:headers_fancybox(jQuery);return false;";
//        ClientScript.RegisterStartupScript(GetType(), "headers-fancybox-startup", @"
//	function headers_fancybox($) {
//		$.fancybox($.extend({ href: 'Headers.aspx' ,type: 'iframe'}, fancybox_options));
//	}
//", true);
		AddButtonControl(CtrlContainer, new Button() { ID = "headers-button", CssClass = "submit-button-class" }, "Headers", btnHeaders_Click);
		AddButtonControl(CtrlContainer, new Button() { ID = "preview-button", CssClass = "submit-button-class" }, "Preview", btnXml_Click);
		AddButtonControl(CtrlContainer, new Button() { ID = "call-button", CssClass = "submit-button-class" }, "Call", btnSend_Click);

		formatResultXml = new CheckBox()
		{
			ID = "format-result-xml",
			Text = "Format Result XML",
			TextAlign = TextAlign.Right
		};
		formatResultXml.Style.Add("vertical-align", "bottom");
		AddControl(CtrlContainer, formatResultXml);

		showResultInPopup = new CheckBox()
		{
			ID = "show-result-popup",
			Text = "Show Result in a Popup",
			TextAlign = TextAlign.Right
		};
		showResultInPopup.Style.Add("vertical-align", "bottom");
		AddControl(CtrlContainer, showResultInPopup);

		bodyChanged = new HiddenField()
		{
			ID = "changed-hidden-field",
			Value = "0"
		};
		AddControl(CtrlContainer, bodyChanged);
		ClientScript.RegisterClientScriptInclude(GetType(), "changed-hidden-field-include",
			ResolveUrl("~/js/app-input-event.js"));
		ClientScript.RegisterStartupScript(GetType(), "changed-hidden-field-startup", @"
	setup_storing_input_time('.input-text-class', '" + bodyChanged.ClientID + @"');
", true);
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
	
	string SoapBuildXml(BaseNode[] nodes, bool soap11, bool formatted)
	{
		XmlDocument doc;
		BuildXml(schemaNodes.Nodes, Soap.CreateDocGetEnvelope(soap11, out doc));
		return doc.GetXml(formatted);
	}

	void UpdateContentLengthHeader(string http_body)
	{
		var cont_len = Headers.Dict.Values.FirstOrDefault(h => "Content-Length".Equals(h.Name));
		if (cont_len != null)
		{
			if (cont_len.Changed == 0 || long.Parse(bodyChanged.Value) >= cont_len.Changed)
				cont_len.Value = "" + Encoding.UTF8.GetByteCount(http_body);
		}
	}

	void btnXml_Click(object sender, EventArgs e)
	{
		var bindingNamespacePrefix = (string)Session["binding-namespace-prefix"];
		var port_url = (Uri)Session["port-url"];
		switch (bindingNamespacePrefix)
		{
			case "soap":
			case "soap12":
				var xml = SoapBuildXml(schemaNodes.Nodes, "soap".Equals(bindingNamespacePrefix), true);
				UpdateContentLengthHeader(xml);
				AddVerticalPadding(Page.Form);
				AddLiteral(Page.Form, "<div id='xml-div'><pre>" +
					Server.HtmlEncode(Http.BuildRequest(port_url, xml, Headers.Dict.Values.Cast<Http.Header>())) +
					"</pre></div>");

				ClientScript.RegisterStartupScript(GetType(), "xml-fancybox-startup", @"
	jQuery(function($) {
		$.fancybox.open($.extend({ type: 'inline', href: '#xml-div' }, get_fancybox_options('80%', '80%')));
	});
", true);
				break;
		}
	}

	void btnSend_Click(object sender, EventArgs e)
	{
		var bindingNamespacePrefix = (string)Session["binding-namespace-prefix"];
		var port_url = (Uri)Session["port-url"];
		switch (bindingNamespacePrefix)
		{
			case "soap":
			case "soap12":
				var xml = SoapBuildXml(schemaNodes.Nodes, "soap".Equals(bindingNamespacePrefix), true);
				UpdateContentLengthHeader(xml);
				string result;
				bool bHttpWebRequest = (port_url.Scheme == Uri.UriSchemeHttps);
//#if HttpWebRequest
				if (bHttpWebRequest)
				{
					try
					{
						var req = Http.CreateRequest(port_url, xml, Headers.Dict.Values.Cast<Http.Header>());
						// Result to string
						result = Http.GetResponseXml(req, formatResultXml.Checked);
					}
					catch (WebException ex)
					{
						result = Http.FormatResponseXml((HttpWebResponse)ex.Response, formatResultXml.Checked);
					}
				}
//#else
				else
				{
					result = Http.SocketSendReceive(port_url.Host, port_url.Port,
						Http.BuildRequest(port_url, xml, Headers.Dict.Values.Cast<Http.Header>()));
					if (formatResultXml.Checked)
						result = Http.FormatSocketXml(result);
				}
//#endif

				AddVerticalPadding(Page.Form);
				AddLiteral(Page.Form, "<div id='xml-div'><pre>" +
					Server.HtmlEncode(result) +
					"</pre></div>");

				if (showResultInPopup.Checked)
					ClientScript.RegisterStartupScript(GetType(), "xml-fancybox-startup", @"
	jQuery(function($) {
		$.fancybox.open($.extend({ type: 'inline', href: '#xml-div' }, get_fancybox_options('80%', '80%')));
	});
", true);
				break;
		}
	}

	void btnHeaders_Click(object sender, EventArgs e)
	{
		var bindingNamespacePrefix = (string)Session["binding-namespace-prefix"];
		var port_url = (Uri)Session["port-url"];
		switch (bindingNamespacePrefix)
		{
			case "soap":
			case "soap12":
				var xml = SoapBuildXml(schemaNodes.Nodes, "soap".Equals(bindingNamespacePrefix), true);
				UpdateContentLengthHeader(xml);

				ClientScript.RegisterStartupScript(GetType(), "headers-fancybox-startup", @"
	jQuery(function($) {
		$.fancybox.open($.extend({ href: 'Headers.aspx' ,type: 'iframe'}, fancybox_options));
	});
", true);
				break;
		}
	}
</script>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head id="Head1" runat="server">
	<title>Web Service Operation</title>
	<style type="text/css">
		body, input, select { font: 8pt Arial; }
		.label-class { /*font: 9pt Arial;*/ font-weight: bold; font-style: italic; }
		.input-image-class, .input-text-class { height: 9pt; border-style: none; vertical-align: middle; }
		.input-text-class { background-color: #dcecfc; width: 250px; }
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
	<%--<script type='text/javascript' src="<%= ResolveUrl("~/js/cookies.js") %>"></script>--%>
</head>
<body>
	<a href='Wsdl.aspx?operation=<%= Server.UrlEncode(Request["operation"]) %>'>&laquo; back to WSDL</a>
	<form id="form1" runat="server">
		<!--<table border="0" width="100%"><tr>
			<td nowrap>Schema URL:&nbsp;</td>
			<td width="100%"><asp:TextBox ID="txSchemaUrl" runat="server" Width="100%"></asp:TextBox></td>
		</tr></table>-->
	</form>
</body>
</html>
