using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace wstester
{

	using System.Xml;

	public static partial class Extensions
	{

		public static XmlQualifiedName GetName(this XmlNode node)
		{
			var ns_attr = node.OwnerDocument.DocumentElement.Attributes["targetNamespace"];
			return new XmlQualifiedName(node.Attributes["name"].Value, ns_attr != null ? ns_attr.Value : "");
		}

		private static bool _CheckNamespace(XmlDocument x, string ns) { return x.DocumentElement.Attributes["targetNamespace"].Value.Equals(ns); }
		private static string _Xpath(string xpath, string name) { return xpath + "[@name='" + name + "']"; }

		public static IEnumerable<XmlNode> SelectNodesByNameGlobal(this IEnumerable<XmlDocument> wsdl_, string xpath, XmlQualifiedName name,
			XmlNamespaceManager nsmgr)
		{
			if (!xpath.StartsWith("/"))
				throw new NotSupportedException("Relative xpath is not supported.");
			return wsdl_.Where(x => _CheckNamespace(x, name.Namespace))
				.SelectMany(x => x.SelectNodes(_Xpath(xpath, name.Name), nsmgr).Cast<XmlNode>());
		}

		public static IEnumerable<XmlNode> SelectNodesByNameFromAttrGlobal(this XmlNode node, string xpath, string attrName, IEnumerable<XmlDocument> wsdl_,
			XmlNamespaceManager nsmgr)
		{
			var name = AttrValueAsName(node, attrName);
			return SelectNodesByNameGlobal(wsdl_, xpath, name, nsmgr);
		}

		public static XmlNode SelectSingleNodeByNameGlobal(this IList<XmlDocument> wsdl_, string xpath, XmlQualifiedName name,
			XmlNamespaceManager nsmgr)
		{
			if (!xpath.StartsWith("/"))
				throw new NotSupportedException("Relative xpath is not supported.");
			XmlNode res = null;
			for (var i = 0; i < wsdl_.Count; i++)
				if (_CheckNamespace(wsdl_[i], name.Namespace) && (res = wsdl_[i].SelectSingleNode(_Xpath(xpath, name.Name), nsmgr)) != null)
					break;
			return res;
		}

		public static XmlNode SelectSingleNodeByNameFromAttrGlobal(this XmlNode node, string xpath, string attrName, IList<XmlDocument> wsdl_,
			XmlNamespaceManager nsmgr)
		{
			var name = AttrValueAsName(node, attrName);
			return SelectSingleNodeByNameGlobal(wsdl_, xpath, name, nsmgr);
		}

		public static XmlQualifiedName AttrValueAsName(this XmlNode node, string attrName)
		{
			var tmp = node.Attributes[attrName].Value.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
			return new XmlQualifiedName(tmp[tmp.Length > 1 ? 1 : 0], node.GetNamespaceOfPrefix(tmp.Length > 1 ? tmp[0] : ""));
		}

		public static XmlNode SelectSingleNodeByValue(this XmlNode node, string xpath, object val,
			XmlNamespaceManager nsmgr)
		{
			return node.SelectSingleNode(string.Format(xpath, val), nsmgr);
		}
	}

	public enum InOut { input, output }
}