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

		public static LiteralControl AddLiteralControl(Control container, string text)
		{
			return (LiteralControl)AddTextControl(container, new LiteralControl(), text);
		}

		public static void AddVerticalPadding(Control container)
		{
			AddLiteralControl(container, // "<div style='height: " + STYLE_PADDING_VERTICAL + ";'></div>");
				"<div class='vertical-margin'></div>");
		}


		protected SchemaNodes schemaNodes;
		protected virtual Control CtrlContainer { get { return Page.Form; } }

		protected void HandlePostCommand(string postParamName, Action<string> postCommandHandler)
		{
			if (Request.Form[postParamName] != null)
				postCommandHandler(Request.Form[postParamName]);
		}

		protected void HandleNodePostCommand(string postParamName, Action<int> postCommandHandler)
		{
			if (Request.Form[postParamName] != null)
			{
				int id = Convert.ToInt32(Request.Form[postParamName]);
				if (schemaNodes.NodeTable.ContainsKey(id))
				{
					postCommandHandler(id);
				}
			}
		}

		protected string GeneratePostCommandScript(string paramName, object paramValue)
		{
			Page.ClientScript.RegisterClientScriptBlock(Page.GetType(), "post-command", @"
	function postCommand(name, value) {
		if (typeof (theForm.onsubmit) === 'function')
			if (theForm.onsubmit() === false)
				return;
		var e = document.createElement('input');
		e.type = 'hidden';
		e.name = name;
		e.value = value;
		theForm.appendChild(e);
		theForm.submit();
	}
", true);
			return "postCommand('" + paramName + @"','" + paramValue + "');return false;";
		}

		protected override void OnPreInit(EventArgs e)
		{
			base.OnPreInit(e);
			// Handle POST commands:
			// add array node
			HandleNodePostCommand("add_array_node", id => ((ArrayNode)schemaNodes.NodeTable[id]).NewContentNode());
			// delete array node
			HandleNodePostCommand("delete_array_node", id => schemaNodes.NodeTable[id].Delete());
			// hide optional node
			HandleNodePostCommand("hide_optional_node", id => ((ContentNode)schemaNodes.NodeTable[id]).ToggleHidden());
			// change node class
			HandlePostCommand("change_node_class", val =>
				{
					var tmp = val.Split('|');
					var id = int.Parse(tmp[0]);
					var qnHash = uint.Parse(tmp[1]);
					var node = (ContentNode)schemaNodes.NodeTable[id];
					node.ChildNodes = node.inheritance[node.SchemaTypeQN = schemaNodes.schemaTypeQNs[qnHash]] ??
						(node.inheritance[node.SchemaTypeQN] = schemaNodes.ProcessSchema(GetSchema().SchemaTypes[node.SchemaTypeQN]));
				});
		}

		protected virtual XmlSchema GetSchema() { throw new NotImplementedException(); }

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
		$pnl.children('span.cont-class').css('display', hidden ? '' : 'none');
		if (hidden)
			$pnl.find('div.panel-class').addBack().each(function() {
				adjustLabelWidth($(this));
			});
	}

	function adjustLabelWidth($pnl) {
		var max_width = 0;
		$pnl.children('span.cont-class:visible').children('nobr').children('span:first-child').each(function() {
			var w = $(this).width();
			if (w > max_width)
				max_width = w;
		}).css({ width: max_width + 'px', display: 'inline-block' });
	}

	$('#" + CtrlContainer.ClientID + @" div.panel-class').each(function() {
		adjustLabelWidth($(this));
	});

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
									OnClientClick = GeneratePostCommandScript("delete_array_node", node.Id)
								};
						}
						else if (contentNode.isOptional) // && contentNode.AllChildNodes().All(n => n.Type != NodeType.ContentNode || ((ContentNode)n).isOptional))
						{
							delete = new ImageButton()
								{
									CssClass = "input-image-class",
									ImageUrl = contentNode.hidden ? "~/Images/NEW-1.gif" : "~/Images/DELETE-1.gif",
									ID = node.Id.ToString() + "_hide_optional",
									OnClientClick = GeneratePostCommandScript("hide_optional_node", node.Id)
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
							AddLiteralControl(pnl, "<div class='title-class'><table border='0' cellspacing='0' cellpadding='0' width='100%'><tr><td style='white-space:nowrap;'>");
							AddLiteralControl(pnl, "&nbsp;<a href='javascript://' onclick='javascript:togglePnl(this);' class='plus-minus-class'>[" + (_hidden ? "+" : "&ndash;") + "]</a>")
								.Visible = !contentNode.hidden;
							AddLiteralControl(pnl, "&nbsp;" + Server.HtmlEncode(contentNode.Name) +
								(("" + contentNode.BaseTypeName).Trim() != "" && !contentNode.Name.Equals(contentNode.BaseTypeName) && !contentNode.BaseSchemaType.HasDerivedTypes ?
								"&nbsp;:&nbsp;<span class='type-class'>" + Server.HtmlEncode(contentNode.BaseTypeName) + "</span>" : "") +
								(contentNode.BaseSchemaType.HasDerivedTypes ?
								"&nbsp;:&nbsp;<a href='javascript://' onclick='javascript:inherit_" + (uint)contentNode.SchemaElement.SchemaTypeQN.GetHashCode() +
								"(this," + node.Id + ");' class='type-selector-class'>" + Server.HtmlEncode(contentNode.TypeName) + "</a>" : "") +
								"</td><td width='100%'><hr class='hr-class'></td><td>");
							if (contentNode.BaseSchemaType.HasDerivedTypes)
							{
								Func<IEnumerable<XmlQualifiedName>, string, string> _menu = null;
								_menu = (_qns, _stuff) => {
									 var res = _qns.Aggregate("", (_res, _qn) => _res + "<li data-qn-hash=\"" + (uint)_qn.GetHashCode() + "\">" + Server.HtmlEncode(_qn.Name) +
										 (contentNode.schemaNodes.schemaTypes[_qn].HasDerivedTypes ?
										 _menu(contentNode.schemaNodes.schemaTypes[_qn].DerivedTypeQNs, "") : "") + "</li>");
									 return res.Length > 0 ? "<ul" + _stuff + ">" + res + "</ul>" : null;
								 };
								ClientScript.RegisterClientScriptBlock(GetType(), "-inheritance", @"
	function _inherit(a, node_id, html) {
		var $a = $(a).css('display', 'none');
		var $menu = $(html).insertAfter(a).first().menu({
			select: function(event, ui) {
				//$a.text(ui.item.contents().filter(function() { return this.nodeType === 3; }).first().text()).css('display', '');
				//$menu.menu('destroy').remove();
				postCommand('change_node_class', node_id + '|' + ui.item.data('qnHash'));
			}
		}).mouseleave(function(event) {
			$a.css('display', '');
			$menu.menu('destroy').remove();
		});
	}
", true);
								ClientScript.RegisterClientScriptBlock(GetType(), "inheritance-" + (uint)contentNode.SchemaElement.SchemaTypeQN.GetHashCode(), @"
	function inherit_" + (uint)contentNode.SchemaElement.SchemaTypeQN.GetHashCode() + @"(a, node_id) {
		_inherit(a, node_id, '" + _menu(new[] { contentNode.SchemaElement.SchemaTypeQN }, /*" id=\"_menu_\""*/ " style=\"display:inline-block;\"") + @"');
	}
", true);
							}
							if (delete != null)
								AddControl(pnl, delete);
							AddLiteralControl(pnl, "</td></tr></table></div>");
							AddVerticalPadding(pnl);
							contentNode.controlID = pnl.ID;
							if (node.ChildNodes != null && node.ChildNodes.Length > 0)
							{
								var cont = new System.Web.UI.HtmlControls.HtmlGenericControl("span") { ViewStateMode = System.Web.UI.ViewStateMode.Disabled, Visible = !contentNode.hidden };
								cont.Attributes["class"] = "cont-class";
								AddControl(pnl, cont);
								if (_hidden)
								{
									cont.Style.Add("display", "none");
									AddLiteralControl(Form, "<input type='hidden' name='_hide_" + pnl.ClientID + "' id='_hide_" + pnl.ClientID + "' />");
								}
								BuildControls(node.ChildNodes, cont);
								AddVerticalPadding(cont);
							}
						}
						else
						{
							AddLiteralControl(container, "<nobr>&nbsp;");
							AddTextControl(container, new Label() { CssClass = "label-class"/*, AssociatedControlID = node.Id.ToString()*/ }, contentNode.Name + ": ");
							AddLiteralControl(container, "<span" + (contentNode.hidden ? " style='visibility:hidden;'" : "") + ">");
							var webCtrl = (WebControl)AddControl(container, contentNode.IsEnumeration ? (Control)new DropDownList() : (Control)new TextBox());
							AddLiteralControl(container, "</span>");
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
							AddLiteralControl(container, "</nobr>	  ");
						}
						break;
					case NodeType.ArrayNode:
						if (container.HasControls() && container.Controls.Cast<Control>().Last() is LinkButton)
							AddLiteralControl(container, "&nbsp;|&nbsp;");
						AddControl(container, new LinkButton()
							{
								ID = node.Id.ToString(),
								OnClientClick = GeneratePostCommandScript("add_array_node", node.Id),
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
							if (!contentNode.SchemaTypeQN.IsEmpty && !contentNode.SchemaTypeQN.Equals(contentNode.SchemaElement.SchemaTypeQN))
							{
								var xsi_ns = "http://www.w3.org/2001/XMLSchema-instance";
								var type_prefix = newNode.GetPrefixOfNamespace(xsi_ns);
								if (!xsi_ns.Equals(newNode.GetNamespaceOfPrefix(type_prefix)))
									type_prefix = "xsi";
								var type_attr = doc.CreateAttribute(type_prefix, "type", xsi_ns);
								newNode.Attributes.Append(type_attr);
								var val_prefix = newNode.GetPrefixOfNamespace(contentNode.SchemaTypeQN.Namespace);
								var defineNs = false;
								if (!contentNode.SchemaTypeQN.Namespace.Equals(newNode.GetNamespaceOfPrefix(val_prefix)))
								{
									val_prefix = "ns" + (++nsInd);
									defineNs = true;
								}
								type_attr.AppendChild(doc.CreateTextNode(("" + val_prefix != "" ? val_prefix + ":" : "") + contentNode.SchemaTypeQN.Name));
								if (defineNs)
								{
									var ns_attr = doc.CreateAttribute("xmlns", val_prefix, "http://www.w3.org/2000/xmlns/");
									newNode.Attributes.Append(ns_attr);
									ns_attr.AppendChild(doc.CreateTextNode(contentNode.SchemaTypeQN.Namespace));
								}
							}
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
