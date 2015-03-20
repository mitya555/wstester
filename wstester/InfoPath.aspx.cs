using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Xml;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using System.Drawing;

using System.Xml.Schema;
using System.Linq;

public partial class InfoPath : System.Web.UI.Page
{
    enum NodeType { ContentNode, ArrayNode, GroupNode, ChoiceNode }

    abstract class BaseNode
    {
        private static int IdentityCounter = 0;
        protected BaseNode(NodeType type)
        {
            this.type = type;
            id = ++IdentityCounter;
            NodeTable[id] = this;
        }
        private NodeType type;
        public NodeType Type { get { return type; } }
        private BaseNode[] childNodes = new BaseNode[] { };
        public BaseNode[] ChildNodes
        {
            get { return childNodes; }
            set
            {
                childNodes = value;
                foreach (BaseNode child in childNodes)
                    child.parentNode = this;
            }
        }
        public BaseNode parentNode;
        private int id;
        public int Id { get { return id; } }
        public abstract BaseNode Clone();
        protected BaseNode[] CloneChildren()
        {
            List<BaseNode> resList = new List<BaseNode>();
            foreach (BaseNode child in childNodes)
                resList.Add(child.Clone());
            return resList.ToArray();
        }
        protected void CloneChildren(BaseNode newNode)
        {
            newNode.ChildNodes = CloneChildren();
        }
    }

    class ContentNode : BaseNode
    {
        public ContentNode() : base(NodeType.ContentNode) { }
        public ContentNode(string elemName) : this() { this.elemName = elemName; }
        public bool isComplexType;
        public bool isArray;
        public bool isOptional;
        public bool hidden; // when the ContentNode is optional it can be hidden so that not to appear in the output
        private string elemName;
        public string Name { get { return elemName; } }
        public Control control;
        public override BaseNode Clone()
        {
            ContentNode resNode = new ContentNode(this.elemName);
            resNode.isComplexType = this.isComplexType;
            resNode.isArray = this.isArray;
            resNode.isOptional = this.isOptional;
            resNode.hidden = this.hidden;
            CloneChildren(resNode);
            return resNode;
        }
    }

    class ArrayNode : BaseNode
    {
        public ArrayNode() : base(NodeType.ArrayNode) { }
        public ArrayNode(ContentNode contentNode) : this() { this.contentNode = contentNode; }
        public ContentNode contentNode; // the ContentNode to clone
        public override BaseNode Clone()
        {
            return new ArrayNode(this.contentNode);
        }
    }

    class GroupNode : BaseNode
    {
        public GroupNode() : base(NodeType.GroupNode) { }
        public GroupNode(BaseNode[] childNodes) : this() { this.ChildNodes = childNodes; }
        public bool isSequence;
        public override BaseNode Clone()
        {
            GroupNode resNode = new GroupNode(CloneChildren());
            resNode.isSequence = this.isSequence;
            return resNode;
        }
    }

    class ChoiceNode : BaseNode
    {
        public ChoiceNode() : base(NodeType.ChoiceNode) { }
        public ChoiceNode(BaseNode[] childNodes) : this() { this.ChildNodes = childNodes; }
        // childNodes are ONLY to be cloned to include into the resulting XML
        // and are NOT to be included themselves
        public override BaseNode Clone()
        {
            return new ChoiceNode(CloneChildren());
        }
    }

    public static Hashtable NodeTable;
    private BaseNode[] Nodes;

    private string TargetNamespace;

    protected void Page_Init(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            NodeTable = new Hashtable();

            string url = "http://localhost:3559/testInfoPath/WebService.asmx?wsdl";
            //url = "http://localhost/testWSBinaryData/Service1.asmx?wsdl";
			//url = "http://maps.dcgis.dc.gov/propertywebservice/SSLSearch.asmx?wsdl";
			//url = "http://citizenatlas.dc.gov/newwebservices/locationverifier.asmx?wsdl";
			//url = "http://10.1.143.178:8080/HubWeb/services/hubservice20?wsdl";

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
            //req.Credentials = new NetworkCredential("Vasya", "Pupkin");
            HttpWebResponse res = (HttpWebResponse)req.GetResponse();

            XmlDocument wsdl = new XmlDocument();
            wsdl.Load(res.GetResponseStream());

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(wsdl.NameTable);
            nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");

            XmlSchemaSet xss = new XmlSchemaSet(wsdl.NameTable);
            foreach (XmlNode xn in wsdl.SelectNodes("//xsd:schema", nsmgr))
                xss.Add( XmlSchema.Read(new XmlNodeReader(xn), null) );
            xss.Compile();

			var schemas = xss.Schemas().Cast<XmlSchema>();
			Session["xmlnss"] = schemas.Select(s => s.TargetNamespace).ToArray();
			var schema = schemas.FirstOrDefault(s => s.TargetNamespace.Equals(Request["ns"]));
			if (schema == null)
				schema = schemas.First();
			Session["xmlns"] = TargetNamespace = schema.TargetNamespace;
            Nodes = ProcessSchema(schema);

			Session["Nodes"] = Nodes;
			Session["TargetNamespace"] = TargetNamespace;
			Session["NodeTable"] = NodeTable;
        }
        else
        {
			Nodes = (BaseNode[])Session["Nodes"];
			TargetNamespace = (string)Session["TargetNamespace"];
			NodeTable = (Hashtable)Session["NodeTable"];
        }

        // add array node
        HandlePostCommand("add_array_node", (int id) =>
        {
            ArrayNode arrayNode = (ArrayNode)NodeTable[id];
            CloneArrayNode(arrayNode);
        });
        // delete array node
        HandlePostCommand("delete_array_node", (int id) =>
        {
            BaseNode node = (BaseNode)NodeTable[id];
            ArrayList al = new ArrayList(node.parentNode.ChildNodes);
            al.Remove(node);
            node.parentNode.ChildNodes = (BaseNode[])al.ToArray(typeof(BaseNode));
            NodeTable.Remove(id);
        });
        // hide optional node
        HandlePostCommand("hide_optional_node", (int id) =>
        {
            ContentNode node = (ContentNode)NodeTable[id];
            node.hidden = !node.hidden;
        });

		var ddlns = new DropDownList();
		foreach (var ns in (string[])Session["xmlnss"])
			ddlns.Items.Add(ns);
		ddlns.SelectedValue = (string)Session["xmlns"];
		ddlns.AutoPostBack = true;
		ddlns.SelectedIndexChanged += new EventHandler(ddlns_SelectedIndexChanged);
		ddlns.Style.Add("margin-bottom", "10px");
		Page.Form.Controls.Add(ddlns);

        BuildControls(Nodes, Page.Form);

        AddButtonControl(Page.Form, new Button(), "XML", new EventHandler(btnXml_Click));

        PageSaveRestoreScrollPosition();
    }

	void ddlns_SelectedIndexChanged(object sender, EventArgs e)
	{
		var query = Request.Url.Query
			.Split(new [] { '?', '&' }, StringSplitOptions.RemoveEmptyEntries)
			.ToDictionary(s => s.Split('=')[0]);
		query[Server.UrlEncode("ns")] = Server.UrlEncode("ns") + "=" +
			Server.UrlEncode(((DropDownList)sender).SelectedValue);
		var url = new UriBuilder(Request.Url);
		url.Query = String.Join("&", query.Values.ToArray());
		Response.Redirect("" + url);
	}

    delegate void PostCommandHandler(int id);

    void HandlePostCommand(string postCommandParamName, PostCommandHandler postCommandHandler)
    {
        if (Request.Form[postCommandParamName] != null)
        {
            int id = Convert.ToInt32(Request.Form[postCommandParamName]);
            if (NodeTable.Contains(id))
            {
                postCommandHandler(id);
            }
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
    }

    void GeneratePostCommandStartupScript(string scriptName, string paramName, object paramValue)
    {
        Page.ClientScript.RegisterStartupScript(Page.GetType(), scriptName, @"
var e = document.createElement('input');
e.type = 'hidden';
e.name = '" + paramName + @"';
e.value = '" + paramValue + @"';
var f = document.forms[0];
f.appendChild(e);
f.submit();
", true);
    }

    int ControlIDToInt32(string controlID)
    {
        return Convert.ToInt32(controlID.Split('_')[0]);
    }

    void GeneratePostCommand(object sender, string scriptName, string paramName)
    {
        int id = ControlIDToInt32(((Control)sender).ID);
        GeneratePostCommandStartupScript(scriptName, paramName, id);
    }

    void btnXml_Click(object sender, EventArgs e)
    {
        XmlDocument doc = new XmlDocument();
        doc.AppendChild(doc.CreateElement("Root", TargetNamespace));
        BuildXml(Nodes, doc.DocumentElement);

        StringWriter stringWriter = new StringWriter();
        XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter);
        xmlTextWriter.Formatting = Formatting.Indented;
        doc.Save(xmlTextWriter);

        AddVerticalPadding(Page.Form);
        AddLiteral(Page.Form, "<div style='border: 1px solid red; color: red;'><pre>" +
            Server.HtmlEncode(stringWriter.ToString()) + "</pre></div>");
    }

    BaseNode[] ProcessSchema(XmlSchemaObject schema)
    {
        if (schema is XmlSchema)
        {
            // process child schema nodes
            List<BaseNode> resList = new List<BaseNode>();
            // for "schema" parent node process only "element" schema nodes
            foreach (XmlSchemaElement schemaElement in ((XmlSchema)schema).Elements.Values)
                resList.AddRange(ProcessSchema(schemaElement));
            // return array of nodes
            return resList.ToArray();
        }
        else if (schema is XmlSchemaParticle)
        {
            if (schema is XmlSchemaElement)
            {
                XmlSchemaElement schemaElement = (XmlSchemaElement)schema;
                XmlSchemaType type = schemaElement.ElementSchemaType;
                // for element, create ContentNode
                ContentNode resNode = new ContentNode(schemaElement.QualifiedName.Name);
                // for complexType element, process child schema nodes
                if (type is XmlSchemaComplexType)
                {
                    resNode.isComplexType = true;
                    resNode.ChildNodes = ProcessSchema(type);
                }
                // is element optional?
                resNode.isOptional = (schemaElement.MinOccurs == 0);
                // for element array, create ArrayNode
                if (schemaElement.MaxOccurs > 1)
                {
                    resNode.isArray = true;
                    return new BaseNode[] { resNode, new ArrayNode((ContentNode)resNode.Clone()) };
                }
                // for scalar return just this node
                return new BaseNode[] { resNode };
            }
            else if (schema is XmlSchemaGroupBase)
            {
                List<BaseNode> resList = new List<BaseNode>();
                // GroupNodes are needed for the choice schema element to choose from
                BaseNode resNode = new GroupNode();
                if (schema is XmlSchemaChoice)
                    resNode = new ChoiceNode();
                else if (schema is XmlSchemaSequence)
                    ((GroupNode)resNode).isSequence = true;
                // process all children
                foreach (XmlSchemaObject schemaObject in ((XmlSchemaGroupBase)schema).Items)
                    resList.AddRange(ProcessSchema(schemaObject));
                // return the node with the processed children
                resNode.ChildNodes = resList.ToArray();
                return new BaseNode[] { resNode };
            }
            else if (schema is XmlSchemaGroupRef)
            {
                return ProcessSchema(((XmlSchemaGroupRef)schema).Particle);
            }
        }
        else if (schema is XmlSchemaComplexType)
        {
            return ProcessSchema(((XmlSchemaComplexType)schema).ContentTypeParticle);
        }
        else if (schema is XmlSchemaGroup)
        {
            return ProcessSchema(((XmlSchemaGroup)schema).Particle);
        }
        return new BaseNode[] { };
    }

	//private const string STYLE_PADDING_VERTICAL = "5px";
	//private const string STYLE_PADDING_LEFT = "16px";
	//private const string STYLE_PADDING_RIGHT = "3px";

    Control AddControl(Control container, Control control)
    {
        container.Controls.Add(control);
        return control;
    }

    Control AddTextControl(Control container, ITextControl control, string text)
    {
        if (text != null)
            control.Text = text;
        return AddControl(container, (Control)control);
    }

    Control AddButtonControl(Control container, IButtonControl control, string text, EventHandler click)
    {
        if (click != null)
            control.Click += click;
        if (text != null)
            control.Text = text;
        return AddControl(container, (Control)control);
    }

    void AddLiteral(Control container, string text)
    {
        AddTextControl(container, new Literal(), text);
    }

    void AddVerticalPadding(Control container)
    {
        AddLiteral(container, // "<div style='height: " + STYLE_PADDING_VERTICAL + ";'></div>");
			"<div class='vertical-margin'></div>");
    }

    void BuildControls(BaseNode[] nodes, Control container)
    {
        ContentNode prevContentNode = null;

        foreach (BaseNode node in nodes)
        {
            switch (node.Type)
            {
                case NodeType.ContentNode:
                    ContentNode contentNode = (ContentNode)node;

                    ImageButton delete = null;
                    if (contentNode.isArray && prevContentNode != null &&
                        prevContentNode.Name == contentNode.Name)
                    {
                        delete = new ImageButton();
						delete.CssClass = "input-image-class";
                        delete.ImageUrl = "~/Images/DELETE-1.gif";
                        delete.Click += (object sender, ImageClickEventArgs e) =>
                            GeneratePostCommand(sender, "DeleteArrayNodeOnStartup", "delete_array_node");
                        delete.ID = node.Id.ToString() + "_delete";
                    }
                    else if (contentNode.isOptional)
                    {
                        delete = new ImageButton();
						delete.CssClass = "input-image-class";
						delete.ImageUrl = (contentNode.hidden ? 
                            "~/Images/NEW-1.gif" : "~/Images/DELETE-1.gif");
                        delete.Click += (object sender, ImageClickEventArgs e) =>
                            GeneratePostCommand(sender, "HideOptionalNodeOnStartup", "hide_optional_node");
                        delete.ID = node.Id.ToString() + "_hide_optional";
                    }

                    if (contentNode.isComplexType)
                    {
                        if (prevContentNode != null && !prevContentNode.isComplexType)
                            AddVerticalPadding(container);
                        Panel pnl = new Panel();
                        pnl.ID = node.Id.ToString();
                        AddVerticalPadding(pnl);
                        AddLiteral(pnl, "<div class='title-class'>" +
                            "<table border='0' cellspacing='0' cellpadding='0' width='100%'><tr><td>");
                        AddLiteral(pnl, "&nbsp;" + contentNode.Name + "&nbsp;");
                        AddLiteral(pnl, "</td><td width='100%'><hr class='hr-class'></td><td>");
                        if (delete != null)
                            AddControl(pnl, delete);
                        AddLiteral(pnl, "</td></tr></table></div>");
                        AddVerticalPadding(pnl);
                        pnl.BorderStyle = BorderStyle.Solid;
                        pnl.BorderWidth = Unit.Pixel(1);
                        pnl.BorderColor = Color.Gray;
						pnl.CssClass = "panel-class";
						//pnl.Style.Add("padding-right", STYLE_PADDING_RIGHT);
						//pnl.Style.Add("padding-left", STYLE_PADDING_LEFT);
                        container.Controls.Add(pnl);
                        AddVerticalPadding(container);
                        contentNode.control = pnl;
                        if (node.ChildNodes != null && node.ChildNodes.Length > 0)
                        {
                            PlaceHolder placeholder = new PlaceHolder();
                            placeholder.Visible = !contentNode.hidden;
                            AddControl(pnl, placeholder);
                            BuildControls(node.ChildNodes, placeholder);
                            AddVerticalPadding(pnl);
                        }
                    }
                    else
                    {
                        AddLiteral(container, "<nobr>&nbsp;");
                        AddTextControl(container, new Label(), ((ContentNode)node).Name + ": ");
                        PlaceHolder placeholder = new PlaceHolder();
                        placeholder.Visible = !contentNode.hidden;
                        AddControl(container, placeholder);
                        (contentNode.control = AddTextControl(placeholder, new TextBox(), null))
                            .ID = node.Id.ToString();
						((WebControl)contentNode.control).CssClass = "input-text-class";
                        if (delete != null)
                            AddControl(container, delete);
                        AddLiteral(container, "</nobr>      ");
                    }
                    break;
                case NodeType.ArrayNode:
                    AddButtonControl(container, new LinkButton(), "add " +
                        ((ArrayNode)node).contentNode.Name, (object sender, EventArgs e) =>
                            GeneratePostCommand(sender, "AddArrayNodeOnStartup", "add_array_node"))
                            .ID = node.Id.ToString();
                    break;
                case NodeType.GroupNode:
                    if (node.ChildNodes != null && node.ChildNodes.Length > 0)
                        BuildControls(node.ChildNodes, container);
                    break;
            }

            prevContentNode = node as ContentNode;
        }
    }

    void CloneArrayNode(ArrayNode arrayNode)
    {
        BaseNode newNode = arrayNode.contentNode.Clone();
        BaseNode parentNode = arrayNode.parentNode;
        List<BaseNode> parentNodeChildrenList = new List<BaseNode>(parentNode.ChildNodes);
        parentNodeChildrenList.Insert(parentNodeChildrenList.IndexOf(arrayNode), newNode);
        parentNode.ChildNodes = parentNodeChildrenList.ToArray();
    }

    void BuildXml(BaseNode[] nodes, XmlNode container)
    {
        foreach (BaseNode node in nodes)
        {
            switch (node.Type)
            {
                case NodeType.ContentNode:
                    ContentNode contentNode = (ContentNode)node;
                    if (!contentNode.hidden)
                    {
                        XmlNode newNode;
                        XmlDocument doc = container.OwnerDocument;
                        container.AppendChild(newNode = doc.CreateElement(contentNode.Name, TargetNamespace));
                        if (contentNode.isComplexType)
                        {
                            if (node.ChildNodes != null)
                                BuildXml(node.ChildNodes, newNode);
                        }
                        else
                            newNode.AppendChild(doc.CreateTextNode(((TextBox)contentNode.control).Text));
                    }
                    break;
                case NodeType.GroupNode:
                    if (node.ChildNodes != null && node.ChildNodes.Length > 0)
                        BuildXml(node.ChildNodes, container);
                    break;
            }
        }
    }

    private void PageSaveRestoreScrollPosition()
    {
        Page.ClientScript.RegisterStartupScript(Page.GetType(), "ScrollPositionStartupScript", @"
var __nonMSDOMBrowser = (window.navigator.appName.toLowerCase().indexOf('explorer') == -1);
function WebForm_GetScrollX() {
    if (__nonMSDOMBrowser) {
        return window.pageXOffset;
    }
    else {
        if (document.documentElement && document.documentElement.scrollLeft) {
            return document.documentElement.scrollLeft;
        }
        else if (document.body) {
            return document.body.scrollLeft;
        }
    }
    return 0;
}
function WebForm_GetScrollY() {
    if (__nonMSDOMBrowser) {
        return window.pageYOffset;
    }
    else {
        if (document.documentElement && document.documentElement.scrollTop) {
            return document.documentElement.scrollTop;
        }
        else if (document.body) {
            return document.body.scrollTop;
        }
    }
    return 0;
}
function WebForm_SaveScrollPosition() {
    if (__nonMSDOMBrowser) {
        theForm.elements['__SCROLLPOSITIONY'].value = window.pageYOffset;
        theForm.elements['__SCROLLPOSITIONX'].value = window.pageXOffset;
    }
    else {
        theForm.__SCROLLPOSITIONX.value = WebForm_GetScrollX();
        theForm.__SCROLLPOSITIONY.value = WebForm_GetScrollY();
    }
    return true;
}
function WebForm_SaveScrollPositionConstantly() {
    WebForm_SaveScrollPosition();
    window.setTimeout('WebForm_SaveScrollPositionConstantly();', 10);
}
function WebForm_RestoreScrollPosition() {
    if (__nonMSDOMBrowser) {
        window.scrollTo(theForm.elements['__SCROLLPOSITIONX'].value, theForm.elements['__SCROLLPOSITIONY'].value);
    }
    else {
        window.scrollTo(theForm.__SCROLLPOSITIONX.value, theForm.__SCROLLPOSITIONY.value);
    }
    return true;
}
var theForm = document.forms[0];
WebForm_RestoreScrollPosition();
WebForm_SaveScrollPositionConstantly();
            ", true);
        Page.ClientScript.RegisterHiddenField("__SCROLLPOSITIONX", Request.Form["__SCROLLPOSITIONX"]);
        Page.ClientScript.RegisterHiddenField("__SCROLLPOSITIONY", Request.Form["__SCROLLPOSITIONY"]);
    }
}
