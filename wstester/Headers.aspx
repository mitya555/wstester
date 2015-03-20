<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Headers.aspx.cs" Inherits="wstester.Headers" 
	MaintainScrollPositionOnPostback="true" %>
<script runat="server">

	protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
	{
		//if ("Insert".Equals(e.CommandName))
		//{
		//    Insert(new Header()
		//    {
		//        Name = ((ITextControl)GridView1.FooterRow.FindControl("TextBox1")).Text,
		//        Value = ((ITextControl)GridView1.FooterRow.FindControl("TextBox2")).Text
		//    });
		//    GridView1.DataBind();
		//}
	}

	private string ControlClientID(object container, string id)
	{
		var cont = container as GridViewRow;
		return cont != null ? cont.FindControl(id).ClientID : null;
	}
</script>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title></title>
    <script type="text/javascript" src="//ajax.googleapis.com/ajax/libs/jquery/1.8.2/jquery.min.js"></script>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    
    	<asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" DataKeyNames="Id" 
			DataSourceID="ObjectDataSource1" ShowHeader="False" 
			onrowdatabound="GridView1_RowDataBound" onrowcommand="GridView1_RowCommand" 
			BorderStyle="None"><%-- ShowFooter="True"--%>
			<Columns>
				<asp:BoundField DataField="Id" HeaderText="Id" SortExpression="Id" 
					Visible="False" />
				<asp:TemplateField HeaderText="Name" SortExpression="Name">
					<EditItemTemplate>
						<asp:TextBox ID="TextBox1" runat="server" Text='<%# Bind("Name") %>' />
					</EditItemTemplate>
					<ItemTemplate>
						<asp:TextBox ID="TextBox1" runat="server" Text='<%# Bind("Name") %>' 
							ReadOnly="true" BorderColor="Transparent" />
					</ItemTemplate>
					<%--<FooterTemplate>
						<asp:TextBox ID="TextBox1" runat="server" Text='<%# Bind("Name") %>' />
					</FooterTemplate>--%>
				</asp:TemplateField>
				<asp:TemplateField HeaderText="Value" SortExpression="Value">
					<EditItemTemplate>
						<asp:TextBox ID="TextBox2" runat="server" Text='<%# Bind("Value") %>' Width="530" />
						<asp:HiddenField ID="ChangedHiddenField" runat="server" Value='<%# Bind("Changed") %>' />
<script type="text/javascript" src='<%= ResolveUrl("~/js/app-input-event.js") %>'></script>
<script type="text/javascript">
	setup_storing_input_time('#<%# ControlClientID(Container, "TextBox2") %>',
		'<%# ControlClientID(Container, "ChangedHiddenField") %>');
</script>
					</EditItemTemplate>
					<ItemTemplate>
						<asp:TextBox ID="TextBox2" runat="server" Text='<%# Bind("Value") %>' 
							ReadOnly="true" BorderColor="Transparent" Width="530" />
					</ItemTemplate>
					<%--<FooterTemplate>
						<asp:TextBox ID="TextBox2" runat="server" Text='<%# Bind("Value") %>' />
					</FooterTemplate>--%>
				</asp:TemplateField>
				<asp:TemplateField ShowHeader="False">
					<EditItemTemplate>
						<asp:LinkButton ID="LinkButton1" runat="server" CausesValidation="True" 
							CommandName="Update" Text='<%# ((Guid)Eval("Id")).Equals(Activator.CreateInstance(typeof(Guid))) ? "Insert" : "Update" %>' />
						&nbsp;<asp:LinkButton ID="LinkButton2" runat="server" CausesValidation="False" 
							CommandName="Cancel" Text="Cancel" />
					</EditItemTemplate>
					<ItemTemplate>
						<asp:LinkButton ID="LinkButton1" runat="server" CausesValidation="False" 
							CommandName="Edit" 
							Text='<%# ((Guid)Eval("Id")).Equals(Activator.CreateInstance(typeof(Guid))) ? "New" : "Edit" %>' />
						&nbsp;<asp:LinkButton ID="LinkButton2" runat="server" CausesValidation="False" 
							CommandName="Delete" Text="Delete" 
							Visible='<%# !((Guid)Eval("Id")).Equals(Activator.CreateInstance(typeof(Guid))) %>' />
					</ItemTemplate>
					<%--<FooterTemplate>
						<asp:LinkButton ID="LinkButton1" runat="server" CausesValidation="True" 
							CommandName="Insert" Text="Insert" />
						&nbsp;<asp:LinkButton ID="LinkButton2" runat="server" CausesValidation="False" 
							CommandName="Cancel" Text="Cancel" />
					</FooterTemplate>--%>
					<ControlStyle Font-Italic="True" Font-Size="8pt" />
					<ItemStyle Wrap="True" />
				</asp:TemplateField>
			</Columns>
		</asp:GridView>
		<asp:ObjectDataSource ID="ObjectDataSource1" runat="server" 
			DataObjectTypeName="wstester.Headers+HttpHeader" DeleteMethod="Delete" 
			InsertMethod="Insert" OldValuesParameterFormatString="original_{0}" 
			SelectMethod="Select" TypeName="wstester.Headers" UpdateMethod="Update" />
    
    </div>
    </form>
</body>
</html>
