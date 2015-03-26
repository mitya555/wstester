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

		public Dictionary<XmlQualifiedName, SchemaType> schemaTypes = new Dictionary<XmlQualifiedName,SchemaType>();
		public Dictionary<XmlQualifiedName, SchemaElement> schemaElements = new Dictionary<XmlQualifiedName,SchemaElement>();

		public static XmlSchema GetSchema(XmlSchemaObject schemaObject)
		{
			while (true)
			{
				if (schemaObject == null || schemaObject is XmlSchema)
					return (XmlSchema)schemaObject;
				schemaObject = schemaObject.Parent;
			}
		}

		protected BaseNode[] ProcessSchema(XmlSchemaObject schema, int recursionDepth)
		{
			if (recursionDepth > 25)
				return null;

			if (schema is XmlSchema)
			{
				// process child schema nodes
				List<BaseNode> resList = new List<BaseNode>();
				// for "schema" parent node process only "element" schema nodes
				foreach (XmlSchemaElement schemaElement in ((XmlSchema)schema).Elements.Values)
					resList.AddRange(ProcessSchema(schemaElement, recursionDepth + 1));
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
				//	resNode.ChildNodes = ProcessSchema(type, recursionDepth + 1);
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
						if (resNode.inheritance == null)
							resNode.ChildNodes = ProcessSchema(type, recursionDepth + 1);
						else
						{
							foreach (var _schemaTypeQN in resNode.inheritance.Keys.ToArray())
								resNode.inheritance[_schemaTypeQN] = ProcessSchema(GetSchema(schema).SchemaTypes[_schemaTypeQN], recursionDepth + 1);
							resNode.ChildNodes = resNode.inheritance[resNode.SchemaTypeQN];
						}
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
						resList.AddRange(ProcessSchema(schemaObject, recursionDepth + 1) ?? new BaseNode[0]);
					// return the node with the processed children
					resNode.ChildNodes = resList.ToArray();
					return new BaseNode[] { resNode };
				}
				else if (schema is XmlSchemaGroupRef)
				{
					return ProcessSchema(((XmlSchemaGroupRef)schema).Particle, recursionDepth + 1);
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
						resList.AddRange(ProcessSchema(attr, recursionDepth + 1));
					// process the particle
					resList.AddRange(ProcessSchema(((XmlSchemaComplexType)schema).ContentTypeParticle, recursionDepth + 1));
					// return the node with the processed children
					resNode.ChildNodes = resList.ToArray();
					return new BaseNode[] { resNode };
				}
				return ProcessSchema(((XmlSchemaComplexType)schema).ContentTypeParticle, recursionDepth + 1);
			}
			else if (schema is XmlSchemaGroup)
			{
				return ProcessSchema(((XmlSchemaGroup)schema).Particle, recursionDepth + 1);
			}
			return null;
		}

		public SchemaNodes LoadXmlSchema(XmlSchemaObject schema)
		{
			auxNodeTable = new Dictionary<int, BaseNode>();
			nodes = ProcessSchema(schema, 1);
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
		private BaseNode[] childNodes;
		public BaseNode[] ChildNodes
		{
			get { return childNodes; }
			set
			{
				childNodes = value;
				if (childNodes != null)
					foreach (BaseNode child in childNodes)
						child.parentNode = this;
			}
		}
		public IEnumerable<BaseNode> AllChildNodes()
		{
			if (childNodes != null)
				foreach (var child in childNodes)
				{
					yield return child;
					foreach (var grandchild in child.AllChildNodes())
						yield return grandchild;
				}
		}
		public int Id { get { return id; } }
		public abstract BaseNode Clone();
		protected static BaseNode[] CloneChildren(BaseNode[] nodes)
		{
			return nodes != null ? nodes.Select(n => n.Clone()).ToArray() : null;
		}
		protected BaseNode[] CloneChildren()
		{
			return CloneChildren(childNodes);
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
		public IEnumerable<XmlQualifiedName> derivedTypeQNs;
		private static IEnumerable<XmlQualifiedName> GetDerivedTypeQNs(XmlSchemaType xmlSchemaType, XmlSchema schema, SchemaNodes schemaNodes)
		{
			var qn = xmlSchemaType.QualifiedName;
			if (qn.IsEmpty)
				return null;

			return schema.SchemaTypes.Values.OfType<XmlSchemaComplexType>()
				.Where(t => t.BaseXmlSchemaType.QualifiedName == qn)
				.Select(t => FromXmlSchemaType(t, schema, schemaNodes).QualifiedName)
				.ToArray();
		}
		public static SchemaType FromXmlSchemaType(XmlSchemaType xmlSchemaType, XmlSchema schema, SchemaNodes schemaNodes)
		{
			var qn = xmlSchemaType.QualifiedName;
			SchemaType res = null;
			if (qn.IsEmpty || !schemaNodes.schemaTypes.ContainsKey(qn))
			{
				res = new SchemaType()
				{
					Name = xmlSchemaType.Name,
					QualifiedName = qn,
					IsSimpleType = xmlSchemaType is XmlSchemaSimpleType,
					Enumeration = xmlSchemaType is XmlSchemaSimpleType && xmlSchemaType.DerivedBy == XmlSchemaDerivationMethod.Restriction ?
						((XmlSchemaSimpleTypeRestriction)((XmlSchemaSimpleType)xmlSchemaType).Content).Facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value).ToArray() : null,
					derivedTypeQNs = GetDerivedTypeQNs(xmlSchemaType, schema, schemaNodes)
				};
				if (!qn.IsEmpty)
					schemaNodes.schemaTypes.Add(qn, res);
			}
			return res ?? schemaNodes.schemaTypes[qn];
		}
	}
	[Serializable]
	public class SchemaElement
	{
		public XmlQualifiedName QualifiedName;
		public XmlQualifiedName SchemaTypeQN;
		public SchemaType SchemaType;
		public XmlSchemaForm Form;
		public static XmlQualifiedName FromXmlSchemaElement(XmlSchemaElement xmlSchemaElement, SchemaNodes schemaNodes)
		{
			return CreateSchemaElement(xmlSchemaElement.QualifiedName, xmlSchemaElement.ElementSchemaType, xmlSchemaElement.Form, SchemaNodes.GetSchema(xmlSchemaElement), schemaNodes);
		}
		public static XmlQualifiedName FromXmlSchemaAttribute(XmlSchemaAttribute xmlSchemaAttribute, SchemaNodes schemaNodes)
		{
			return CreateSchemaElement(xmlSchemaAttribute.QualifiedName, xmlSchemaAttribute.AttributeSchemaType, xmlSchemaAttribute.Form, SchemaNodes.GetSchema(xmlSchemaAttribute), schemaNodes);
		}
		public static XmlQualifiedName CreateSchemaElement(XmlQualifiedName qn, XmlSchemaType xst, XmlSchemaForm xsf, XmlSchema schema, SchemaNodes schemaNodes)
		{
			if (!schemaNodes.schemaElements.ContainsKey(qn))
			{
				var st = SchemaType.FromXmlSchemaType(xst, schema, schemaNodes);
				schemaNodes.schemaElements.Add(qn, new SchemaElement()
				{
					QualifiedName = qn,
					SchemaTypeQN = st.QualifiedName,
					SchemaType = st.QualifiedName.IsEmpty ? st : null,
					Form = xsf
				});
			}
			return qn;
		}
	}

	[Serializable]
	public class ContentNode : BaseNode
	{
		private ContentNode(XmlQualifiedName schemaElementQN, string targetNamespace, SchemaNodes schemaNodes) :
			base(NodeType.ContentNode, schemaNodes)
		{
			this.schemaElementQN = schemaElementQN;
			this.schemaTypeQN = SchemaElement.SchemaTypeQN;
			this.targetNamespace = targetNamespace;
		}
		public ContentNode(XmlSchemaElement xmlSchemaElement, string targetNamespace, SchemaNodes schemaNodes) :
			this(SchemaElement.FromXmlSchemaElement(xmlSchemaElement, schemaNodes), targetNamespace, schemaNodes)
		{
			BuildInheritance(SchemaElement.SchemaTypeQN, true);
		}
		public ContentNode(XmlSchemaAttribute xmlSchemaAttribute, string targetNamespace, SchemaNodes schemaNodes) :
			this(SchemaElement.FromXmlSchemaAttribute(xmlSchemaAttribute, schemaNodes), targetNamespace, schemaNodes)
		{
			BuildInheritance(SchemaElement.SchemaTypeQN, true);
		}
		private void BuildInheritance(XmlQualifiedName schemaTypeQN, bool outerScope)
		{
			if (outerScope)
			{
				if (schemaTypeQN.IsEmpty)
					return;
				inheritance = new Dictionary<XmlQualifiedName, BaseNode[]>();
			}
			inheritance.Add(schemaTypeQN, null);
			foreach (var _schemaTypeQN in schemaNodes.schemaTypes[schemaTypeQN].derivedTypeQNs)
				BuildInheritance(_schemaTypeQN, false);
			if (outerScope && inheritance.Count == 1)
				inheritance = null;
		}
		public bool isAttribute;
		public bool isComplexType;
		public bool isArray;
		public bool isOptional;
		public bool hidden; // when the ContentNode is optional it can be hidden so that not to appear in the output
		public bool ToggleHidden() { return (this.hidden = !this.hidden); }
		private string targetNamespace;
		public string controlID;
		private XmlQualifiedName schemaElementQN, schemaTypeQN;
		public XmlQualifiedName SchemaTypeQN { get { return schemaTypeQN; } set { schemaTypeQN = value; } }
		private SchemaElement SchemaElement { get { return schemaNodes.schemaElements[schemaElementQN]; } }
		public SchemaType SchemaType { get { return SchemaElement.SchemaType ?? schemaNodes.schemaTypes[schemaTypeQN]; } }
		public string Name { get { return schemaElementQN.Name; } }
		public string LocalName { get { return schemaElementQN.Name; } }
		public string NamespaceUri { get { return (!SchemaType.IsSimpleType && SchemaType.Name != null ? schemaTypeQN : schemaElementQN).Namespace; } }
		public XmlSchemaForm SchemaForm { get { return SchemaElement.Form; } }
		public string TypeName { get { return schemaTypeQN.IsEmpty ? null : schemaTypeQN.Name; } }
		public bool IsEnumeration { get { return SchemaType.Enumeration != null && SchemaType.Enumeration.Length > 0; } }
		public string TargetNamespace { get { return targetNamespace; } }
		public IDictionary<XmlQualifiedName, BaseNode[]> inheritance;
		public override BaseNode Clone()
		{
			ContentNode resNode = new ContentNode(schemaElementQN, TargetNamespace, schemaNodes);
			resNode.isAttribute = this.isAttribute;
			resNode.isComplexType = this.isComplexType;
			resNode.isArray = this.isArray;
			resNode.isOptional = this.isOptional;
			resNode.hidden = this.hidden;
			if (this.inheritance == null)
				resNode.ChildNodes = CloneChildren();
			else
			{
				resNode.inheritance = new Dictionary<XmlQualifiedName, BaseNode[]>();
				foreach (var _schemaTypeQN in this.inheritance.Keys)
					resNode.inheritance[_schemaTypeQN] = CloneChildren(this.inheritance[_schemaTypeQN]);
				resNode.ChildNodes = resNode.inheritance[resNode.schemaTypeQN = this.schemaTypeQN];
			}
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
