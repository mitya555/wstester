using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Xml;
using System.Xml.Schema;
using System.Drawing;

namespace wstester
{
	public partial class XmlPageBase : System.Web.UI.Page
	{

		//private const string STYLE_PADDING_VERTICAL = "5px";
		//private const string STYLE_PADDING_LEFT = "16px";
		//private const string STYLE_PADDING_RIGHT = "3px";

		public static Control AddControl(Control container, Control control)
		{
			container.Controls.Add(control);
			return control;
		}

		public static Control AddTextControl(Control container, ITextControl control, string text)
		{
			if (text != null)
				control.Text = text;
			return AddControl(container, (Control)control);
		}

		public static Control AddButtonControl(Control container, IButtonControl control, string text, EventHandler click)
		{
			if (click != null)
				control.Click += click;
			if (text != null)
				control.Text = text;
			return AddControl(container, (Control)control);
		}

		public static LiteralControl AddLiteral(Control container, string text)
		{
			return (LiteralControl)AddTextControl(container, new LiteralControl(), text);
		}

		public static void AddVerticalPadding(Control container)
		{
			AddLiteral(container, // "<div style='height: " + STYLE_PADDING_VERTICAL + ";'></div>");
				"<div class='vertical-margin'></div>");
		}


		protected SchemaNodes schemaNodes;
		protected virtual Control CtrlContainer { get { return Page.Form; } }

		protected void HandlePostCommand(string postCommandParamName, Action<int> postCommandHandler)
		{
			if (Request.Form[postCommandParamName] != null)
			{
				int id = Convert.ToInt32(Request.Form[postCommandParamName]);
				if (schemaNodes.NodeTable.ContainsKey(id))
				{
					postCommandHandler(id);
				}
			}
		}

		string GeneratePostCommandScript(string scriptName, string paramName, object paramValue)
		{
			Page.ClientScript.RegisterClientScriptBlock(Page.GetType(), "NodePostCommand", @"function NodePostCommand(name, id) {
	if (typeof (theForm.onsubmit) === 'function')
		if (theForm.onsubmit() === false)
			return;
	var e = document.createElement('input');
	e.type = 'hidden';
	e.name = name;
	e.value = id;
	theForm.appendChild(e);
	theForm.submit();
}
", true);
			Page.ClientScript.RegisterClientScriptBlock(Page.GetType(), scriptName, @"function " + scriptName +
				@"(id) { NodePostCommand('" + paramName + @"', id); }
", true);
			return scriptName + "('" + paramValue + "'); return false;";
		}

		protected override void OnPreInit(EventArgs e)
		{
			base.OnPreInit(e);
			// Handle POST commands:
			// add array node
			HandlePostCommand("add_array_node", id => ((ArrayNode)schemaNodes.NodeTable[id]).NewContentNode());
			// delete array node
			HandlePostCommand("delete_array_node", id => schemaNodes.NodeTable[id].Delete());
			// hide optional node
			HandlePostCommand("hide_optional_node", id => ((ContentNode)schemaNodes.NodeTable[id]).ToggleHidden());
		}

		protected override void OnInit(EventArgs e)
		{
			BuildControls(schemaNodes.Nodes, CtrlContainer);
			base.OnInit(e);
			ClientScript.RegisterStartupScript(GetType(), "xml-page-base-startup-script", @"
	function togglePnl(a) {
		var $pnl = $(a).parents('div.panel-class:first'), pnl_id = $pnl[0].id, $form = $(theForm),
			$hidden = $form.children('input#_hide_' + pnl_id), hidden = ($hidden.length > 0);
		if (!hidden)
			$form.append('<input type=""hidden"" name=""_hide_' + pnl_id + '"" id=""_hide_' + pnl_id + '"" />');
		else
			$hidden.remove();
		$(a).html(hidden ? '[&ndash;]' : '[+]');
		$pnl.children('span').css('display', hidden ? '' : 'none');
		adjustLabelWidth();
	}

	function adjustLabelWidth() {
		$('#" + CtrlContainer.ClientID + @" div.panel-class').each(function() {
			var max_width = 0;
			$(this).children('span:visible').children('nobr').children('span:first-child').each(function() {
				var w = $(this).width();
				if (w > max_width)
					max_width = w;
			}).css({ width: max_width + 'px', display: 'inline-block' });
		});
	}

	adjustLabelWidth();

	$('input, textarea').placeholder();
", true);

			Response.Cache.SetCacheability(HttpCacheability.NoCache);
			Response.Cache.SetNoStore();
		}

		protected void BuildControls(BaseNode[] nodes, Control container)
		{
			ContentNode prevContentNode = null;

			foreach (BaseNode node in nodes)
			{
				switch (node.Type)
				{
					case NodeType.ContentNode:
						ContentNode contentNode = (ContentNode)node;

						ImageButton delete = null;
						if (contentNode.isArray) // && prevContentNode != null && prevContentNode.Name == contentNode.Name)
						{
							delete = new ImageButton()
								{
									CssClass = "input-image-class",
									ImageUrl = "~/Images/DELETE-1.gif",
									ID = node.Id.ToString() + "_delete",
									OnClientClick = GeneratePostCommandScript("DeleteArrayNodePostCommand", "delete_array_node", node.Id)
								};
						}
						else if (contentNode.isOptional) // && contentNode.AllChildNodes().All(n => n.Type != NodeType.ContentNode || ((ContentNode)n).isOptional))
						{
							delete = new ImageButton()
								{
									CssClass = "input-image-class",
									ImageUrl = contentNode.hidden ? "~/Images/NEW-1.gif" : "~/Images/DELETE-1.gif",
									ID = node.Id.ToString() + "_hide_optional",
									OnClientClick = GeneratePostCommandScript("HideOptionalNodePostCommand", "hide_optional_node", node.Id)
								};
						}
						else if (!contentNode.isComplexType)
						{
							delete = new ImageButton()
								{
									CssClass = "input-image-class",
									ImageUrl = "~/Images/BLANK.gif",
									Enabled = false,
									ID = node.Id.ToString() + "_blank"
								};
						}

						if (contentNode.isComplexType)
						{
							if (prevContentNode != null && !prevContentNode.isComplexType)
								AddVerticalPadding(container);
							Panel pnl = new Panel()
								{
									ID = node.Id.ToString(),
									BorderStyle = BorderStyle.Solid,
									BorderWidth = Unit.Pixel(1),
									BorderColor = Color.Gray,
									CssClass = "panel-class"
								};
							container.Controls.Add(pnl);
							AddVerticalPadding(container);
							var _hidden = (Request.Form["_hide_" + pnl.ClientID] != null);
							//pnl.Style.Add("padding-right", STYLE_PADDING_RIGHT);
							//pnl.Style.Add("padding-left", STYLE_PADDING_LEFT);
							AddVerticalPadding(pnl);
							AddLiteral(pnl, "<div class='title-class'><table border='0' cellspacing='0' cellpadding='0' width='100%'><tr><td style='white-space:nowrap;'>");
							AddLiteral(pnl, "&nbsp;<a href='javascript://' onclick='javascript:togglePnl(this);'>[" + (_hidden ? "+" : "&ndash;") + "]</a>").Visible = !contentNode.hidden;
							AddLiteral(pnl, "&nbsp;" + Server.HtmlEncode(contentNode.Name) +
								(!contentNode.Name.Equals(contentNode.TypeName) && ("" + contentNode.TypeName).Trim() != "" ? "&nbsp;:&nbsp;" + Server.HtmlEncode(contentNode.TypeName) : "") +
								"</td><td width='100%'><hr class='hr-class'></td><td>");
							if (delete != null)
								AddControl(pnl, delete);
							AddLiteral(pnl, "</td></tr></table></div>");
							AddVerticalPadding(pnl);
							contentNode.controlID = pnl.ID;
							if (node.ChildNodes != null && node.ChildNodes.Length > 0)
							{
								var cont = new System.Web.UI.HtmlControls.HtmlGenericControl("span") { ViewStateMode = System.Web.UI.ViewStateMode.Disabled, Visible = !contentNode.hidden };
								AddControl(pnl, cont);
								if (_hidden)
								{
									cont.Style.Add("display", "none");
									AddLiteral(Form, "<input type='hidden' name='_hide_" + pnl.ClientID + "' id='_hide_" + pnl.ClientID + "' />");
								}
								BuildControls(node.ChildNodes, cont);
								AddVerticalPadding(cont);
							}
						}
						else
						{
							AddLiteral(container, "<nobr>&nbsp;");
							AddTextControl(container, new Label() { CssClass = "label-class"/*, AssociatedControlID = node.Id.ToString()*/ }, contentNode.Name + ": ");
							AddLiteral(container, "<span" + (contentNode.hidden ? " style='visibility:hidden;'" : "") + ">");
							var webCtrl = (WebControl)AddControl(container, contentNode.IsEnumeration ? (Control)new DropDownList() : (Control)new TextBox());
							AddLiteral(container, "</span>");
							webCtrl.ViewStateMode = System.Web.UI.ViewStateMode.Enabled;
							contentNode.controlID = webCtrl.ID = node.Id.ToString();
							webCtrl.CssClass = "input-text-class" + (contentNode.IsEnumeration ? " input-select-class" : "");
							if (("" + contentNode.TypeName).Trim() != "")
								webCtrl.Attributes.Add("placeholder", contentNode.TypeName);
							if (contentNode.IsEnumeration)
								foreach (var item in new string[] { "" }.Concat(contentNode.SchemaType.Enumeration))
									((DropDownList)webCtrl).Items.Add(item);
							if (delete != null)
								AddControl(container, delete);
							AddLiteral(container, "</nobr>	  ");
						}
						break;
					case NodeType.ArrayNode:
						if (container.HasControls() && container.Controls.Cast<Control>().Last() is LinkButton)
							AddLiteral(container, "&nbsp;|&nbsp;");
						AddControl(container, new LinkButton()
							{
								ID = node.Id.ToString(),
								OnClientClick = GeneratePostCommandScript("AddArrayNodePostCommand", "add_array_node", node.Id),
								Text = "new &lt;" + ((ArrayNode)node).contentNode.Name + "&gt;"
							});
						break;
					case NodeType.GroupNode:
					case NodeType.ChoiceNode:
						if (node.ChildNodes != null && node.ChildNodes.Length > 0)
							BuildControls(node.ChildNodes, container);
						break;
				}

				prevContentNode = node as ContentNode;
			}
		}

		protected void BuildXml(BaseNode[] nodes, XmlNode container)
		{
			var nsInd = 0;
			foreach (BaseNode node in nodes)
			{
				switch (node.Type)
				{
					case NodeType.ContentNode:
						ContentNode contentNode = (ContentNode)node;
						if (!contentNode.hidden)
						{
							var doc = container is XmlDocument ? (XmlDocument)container : container.OwnerDocument;
							var ns = contentNode.NamespaceUri;
							var prefix = container.GetPrefixOfNamespace(ns);
							Func<string> getPrefix = () => "" + prefix != "" ? prefix : contentNode.SchemaForm == XmlSchemaForm.Qualified ? "ns" + (++nsInd) : null;
							var newNode = contentNode.isAttribute ?
								container.Attributes.Append(doc.CreateAttribute(getPrefix(), contentNode.Name, ns)) :
								container.AppendChild(doc.CreateElement(getPrefix(), contentNode.Name, ns));
							if (contentNode.isComplexType)
							{
								if (node.ChildNodes != null)
									BuildXml(node.ChildNodes, newNode);
							}
							else
								newNode.AppendChild(doc.CreateTextNode(contentNode.IsEnumeration ?
									((DropDownList)CtrlContainer.FindControl(contentNode.controlID)).SelectedValue :
									((TextBox)CtrlContainer.FindControl(contentNode.controlID)).Text));
						}
						break;
					case NodeType.GroupNode:
					case NodeType.ChoiceNode:
						if (node.ChildNodes != null && node.ChildNodes.Length > 0)
							BuildXml(node.ChildNodes, container);
						break;
				}
			}
		}
	}
}
