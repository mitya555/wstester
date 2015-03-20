using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Xml;
using System.Xml.Schema;

namespace wstester
{
	[Serializable]
	public class SchemaNodes
	{
		internal int identityCounter;
		protected IDictionary<int, BaseNode> auxNodeTable;
		protected BaseNode[] nodes;

		public IDictionary<int, BaseNode> NodeTable { get { return auxNodeTable; } }
		public BaseNode[] Nodes { get { return nodes; } }

		private static XmlSchema GetSchema(XmlSchemaObject schemaObject)
		{
			while (true)
			{
				if (schemaObject == null || schemaObject is XmlSchema)
					return (XmlSchema)schemaObject;
				schemaObject = schemaObject.Parent;
			}
		}

		protected BaseNode[] ProcessSchema(XmlSchemaObject schema)
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
			else if (schema is XmlSchemaAttribute)
			{
				XmlSchemaAttribute schemaAttribute = (XmlSchemaAttribute)schema;
				XmlSchemaType type = schemaAttribute.AttributeSchemaType;
				// for element, create ContentNode
				ContentNode resNode = new ContentNode(schemaAttribute, GetSchema(schema).TargetNamespace, this) { isAttribute = true };
				// for complexType element, process child schema nodes
				//if (type is XmlSchemaComplexType)
				//{
				//	resNode.isComplexType = true;
				//	resNode.ChildNodes = ProcessSchema(type);
				//}
				// is element optional?
				resNode.isOptional = (schemaAttribute.Use == XmlSchemaUse.Optional);
				//// for element array, create ArrayNode
				//if (schemaAttribute.MaxOccurs > 1)
				//{
				//	resNode.isArray = true;
				//	return new BaseNode[] { resNode, new ArrayNode((ContentNode)resNode.Clone(), this) };
				//}
				// for scalar return just this node
				return new BaseNode[] { resNode };
			}
			else if (schema is XmlSchemaParticle)
			{
				if (schema is XmlSchemaElement)
				{
					XmlSchemaElement schemaElement = (XmlSchemaElement)schema;
					XmlSchemaType type = schemaElement.ElementSchemaType;
					// for element, create ContentNode
					ContentNode resNode = new ContentNode(schemaElement, GetSchema(schema).TargetNamespace, this);
					// for complexType element, process child schema nodes
					if (type is XmlSchemaComplexType)
					{
						resNode.isComplexType = true;
						resNode.ChildNodes = ProcessSchema(type);
					}
					// is element optional?
					resNode.isOptional = (schemaElement.MinOccurs == 0 || (schema.Parent is XmlSchemaChoice && ((XmlSchemaChoice)schema.Parent).MinOccurs == 0));
					// for element array, create ArrayNode
					if (schemaElement.MaxOccurs > 1 || (schema.Parent is XmlSchemaChoice && ((XmlSchemaChoice)schema.Parent).MaxOccurs > 1))
					{
						resNode.isArray = true;
						return new BaseNode[] { /*resNode,*/ new ArrayNode((ContentNode)resNode.Clone(), this) };
					}
					// for scalar return just this node
					return new BaseNode[] { resNode };
				}
				else if (schema is XmlSchemaGroupBase)
				{
					List<BaseNode> resList = new List<BaseNode>();
					// GroupNodes are needed for the choice schema element to choose from
					var resNode = schema is XmlSchemaChoice ? (BaseNode)new ChoiceNode(this) :
						new GroupNode(this) { isSequence = (schema is XmlSchemaSequence) };
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
				if (((XmlSchemaComplexType)schema).Attributes.Count > 0)
				{
					List<BaseNode> resList = new List<BaseNode>();
					BaseNode resNode = new GroupNode(this);
					// process all attributes
					foreach (var attr in ((XmlSchemaComplexType)schema).Attributes)
						resList.AddRange(ProcessSchema(attr));
					// process the particle
					resList.AddRange(ProcessSchema(((XmlSchemaComplexType)schema).ContentTypeParticle));
					// return the node with the processed children
					resNode.ChildNodes = resList.ToArray();
					return new BaseNode[] { resNode };
				}
				return ProcessSchema(((XmlSchemaComplexType)schema).ContentTypeParticle);
			}
			else if (schema is XmlSchemaGroup)
			{
				return ProcessSchema(((XmlSchemaGroup)schema).Particle);
			}
			return new BaseNode[] { };
		}

		public SchemaNodes LoadXmlSchema(XmlSchemaObject schema)
		{
			auxNodeTable = new Dictionary<int, BaseNode>();
			nodes = ProcessSchema(schema);
			return this;
		}
		public static SchemaNodes FromXmlSchema(XmlSchemaObject schema) { return new SchemaNodes().LoadXmlSchema(schema); }
	}

	public enum NodeType { ContentNode, ArrayNode, GroupNode, ChoiceNode }

	[Serializable]
	public abstract class BaseNode
	{
		private NodeType type;
		private int id;
		protected SchemaNodes schemaNodes;
		public BaseNode parentNode;
		protected BaseNode(NodeType type, SchemaNodes schemaNodes)
		{
			this.type = type;
			this.schemaNodes = schemaNodes;
			id = ++schemaNodes.identityCounter;
			if (schemaNodes.NodeTable != null)
				schemaNodes.NodeTable[id] = this;
		}
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
		public IEnumerable<BaseNode> AllChildNodes()
		{
			foreach (var child in childNodes)
			{
				yield return child;
				foreach (var grandchild in child.AllChildNodes())
					yield return grandchild;
			}
		}
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
		public void Delete()
		{
			var list = new List<BaseNode>(this.parentNode.ChildNodes);
			list.Remove(this);
			this.parentNode.ChildNodes = list.ToArray();
			schemaNodes.NodeTable.Remove(this.id);
		}
	}

	[Serializable]
	public class SchemaType
	{
		public string Name;
		public XmlQualifiedName QualifiedName;
		public string[] Enumeration;
		public bool IsSimpleType;
		public static SchemaType FromXmlSchemaType(XmlSchemaType xmlSchemaType)
		{
			return new SchemaType() { Name = xmlSchemaType.Name, QualifiedName = xmlSchemaType.QualifiedName, IsSimpleType = xmlSchemaType is XmlSchemaSimpleType,
				Enumeration = xmlSchemaType is XmlSchemaSimpleType && xmlSchemaType.DerivedBy == XmlSchemaDerivationMethod.Restriction ?
				((XmlSchemaSimpleTypeRestriction)((XmlSchemaSimpleType)xmlSchemaType).Content).Facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value).ToArray() :
				null };
		}
	}
	[Serializable]
	class SchemaElement
	{
		public XmlQualifiedName QualifiedName;
		public SchemaType ElementSchemaType;
		public XmlSchemaForm Form;
		public static SchemaElement FromXmlSchemaElement(XmlSchemaElement xmlSchemaElement)
		{
			return new SchemaElement() { QualifiedName = xmlSchemaElement.QualifiedName,
				ElementSchemaType = SchemaType.FromXmlSchemaType(xmlSchemaElement.ElementSchemaType), Form = xmlSchemaElement.Form };
		}
		public static SchemaElement FromXmlSchemaAttribute(XmlSchemaAttribute xmlSchemaAttribute)
		{
			return new SchemaElement()
			{
				QualifiedName = xmlSchemaAttribute.QualifiedName,
				ElementSchemaType = SchemaType.FromXmlSchemaType(xmlSchemaAttribute.AttributeSchemaType),
				Form = xmlSchemaAttribute.Form
			};
		}
	}

	[Serializable]
	public class ContentNode : BaseNode
	{
		private ContentNode(SchemaElement schemaElement, string targetNamespace, SchemaNodes schemaNodes) :
			base(NodeType.ContentNode, schemaNodes) { this.schemaElement = schemaElement; this.targetNamespace = targetNamespace; }
		public ContentNode(XmlSchemaElement xmlSchemaElement, string targetNamespace, SchemaNodes schemaNodes) :
			this(SchemaElement.FromXmlSchemaElement(xmlSchemaElement), targetNamespace, schemaNodes) { }
		public ContentNode(XmlSchemaAttribute xmlSchemaAttribute, string targetNamespace, SchemaNodes schemaNodes) :
			this(SchemaElement.FromXmlSchemaAttribute(xmlSchemaAttribute), targetNamespace, schemaNodes) { }
		public bool isAttribute;
		public bool isComplexType;
		public bool isArray;
		public bool isOptional;
		public bool hidden; // when the ContentNode is optional it can be hidden so that not to appear in the output
		public bool ToggleHidden() { return (this.hidden = !this.hidden); }
		private string targetNamespace;
		public string controlID;
		private SchemaElement schemaElement;
		public string Name { get { return schemaElement.QualifiedName.Name; } }
		public string LocalName { get { return schemaElement.QualifiedName.Name; } }
		public string NamespaceUri { get { return (!schemaElement.ElementSchemaType.IsSimpleType && schemaElement.ElementSchemaType.Name != null ? schemaElement.ElementSchemaType.QualifiedName : schemaElement.QualifiedName).Namespace; } }
		public XmlSchemaForm SchemaForm { get { return schemaElement.Form; } }
		public string TypeName { get { return schemaElement.ElementSchemaType.QualifiedName.IsEmpty ? null : schemaElement.ElementSchemaType.QualifiedName.Name; } }
		public SchemaType SchemaType { get { return schemaElement.ElementSchemaType; } }
		public bool IsEnumeration { get { return SchemaType.Enumeration != null && SchemaType.Enumeration.Length > 0; } }
		public string TargetNamespace { get { return targetNamespace; } }
		public override BaseNode Clone()
		{
			ContentNode resNode = new ContentNode(schemaElement, TargetNamespace, schemaNodes);
			resNode.isAttribute = this.isAttribute;
			resNode.isComplexType = this.isComplexType;
			resNode.isArray = this.isArray;
			resNode.isOptional = this.isOptional;
			resNode.hidden = this.hidden;
			CloneChildren(resNode);
			return resNode;
		}
	}

	[Serializable]
	public class ArrayNode : BaseNode
	{
		public ArrayNode(ContentNode contentNode, SchemaNodes schemaNodes) :
			base(NodeType.ArrayNode, schemaNodes) { this.contentNode = contentNode; }
		public ContentNode contentNode; // the ContentNode to clone
		public override BaseNode Clone()
		{
			return new ArrayNode(this.contentNode, schemaNodes);
		}
		public ContentNode NewContentNode()
		{
			var newNode = (ContentNode)this.contentNode.Clone();
			var parentNode = this.parentNode;
			List<BaseNode> parentNodeChildrenList = new List<BaseNode>(parentNode.ChildNodes);
			if (parentNode is ChoiceNode)
				parentNodeChildrenList.Insert(parentNodeChildrenList.IndexOf(parentNodeChildrenList.First(n => n is ArrayNode)), newNode);
			else
				parentNodeChildrenList.Insert(parentNodeChildrenList.IndexOf(this), newNode);
			parentNode.ChildNodes = parentNodeChildrenList.ToArray();
			return newNode;
		}
	}

	[Serializable]
	public class GroupNode : BaseNode
	{
		public GroupNode(SchemaNodes schemaNodes) : base(NodeType.GroupNode, schemaNodes) { }
		protected GroupNode(BaseNode[] childNodes, SchemaNodes schemaNodes) :
			this(schemaNodes) { this.ChildNodes = childNodes; }
		public bool isSequence;
		public override BaseNode Clone()
		{
			GroupNode resNode = new GroupNode(this.CloneChildren(), schemaNodes);
			resNode.isSequence = this.isSequence;
			return resNode;
		}
	}

	[Serializable]
	public class ChoiceNode : BaseNode
	{
		public ChoiceNode(SchemaNodes schemaNodes) : base(NodeType.ChoiceNode, schemaNodes) { }
		protected ChoiceNode(BaseNode[] childNodes, SchemaNodes schemaNodes) :
			this(schemaNodes) { this.ChildNodes = childNodes; }
		// childNodes are ONLY to be cloned to include into the resulting XML
		// and are NOT to be included themselves
		public override BaseNode Clone()
		{
			return new ChoiceNode(this.CloneChildren(), schemaNodes);
		}
	}
}
