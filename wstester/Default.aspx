<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="wstester._Default" %>
<script runat="server">

	protected void LinkButton1_Click(object sender, EventArgs e)
	{
		Response.AddHeader("Access-Control-Allow-Origin", wsdl.Text);
		ClientScript.RegisterStartupScript(GetType(), "read-wsdl-on_startup", @"
//$.support.cors = true;
//$.ajax({
//	url: $('#" + wsdl.ClientID + @"').val(),
//	success: function(data, status, xhr) {
//		$('#result').text(data.xml);
//	},
//	error: function(xhr, status, error) {
//		alert(error);
//	}
//});
var client = new XMLHttpRequest();
client.open('GET', $('#" + wsdl.ClientID + @"').val());
client.onreadystatechange = function() {
	alert('readystatechange');
};
client.onload = function() {
	alert('load');
};
client.onerror = function() {
	alert('error');
};
client.onabort = function() {
	alert('abort');
};
client.send();
", true);		
	}
</script>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>WSDL/WADL</title>
    <script type="text/javascript" src="//ajax.googleapis.com/ajax/libs/jquery/1.8.1/jquery.min.js"></script>
    <script type="text/javascript">
    	$.support.cors = true;
    	function getWsdl() {
    		$.ajax({
    			url: $('#<%= wsdl.ClientID %>').val(),
    			dataType: 'text',
    			//crossDomain: true,
    			success: function(data, status, xhr) {
    				$('#result').text(data);
    			},
    			error: function(xhr, status, error) {
    				alert(error);
    			}
    		});
    	}
    </script>
	<style type="text/css">
		body {
			font-family: Arial;
			font-size: 10pt;
		}
	</style>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    
    	WSDL/WADL URL: 		<asp:TextBox ID="wsdl" runat="server" Width="570px"></asp:TextBox> 		
    	<a href="javascript:getWsdl();">get WSDL/WADL</a><%--<asp:LinkButton 
			ID="LinkButton1" runat="server" onclick="LinkButton1_Click">get WSDL/WADL</asp:LinkButton>--%>
		<pre id="result"></pre>
    </div>
    </form>
</body>
</html>
