using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace wstester
{
	using System.IO;
	using System.Xml;

	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Formatters.Binary;

	using System.Xml.Schema;
	using System.Runtime.Serialization.Json;

	public partial class Xsd : System.Web.UI.Page
	{
		[Serializable]
		public class Element
		{
			//public XmlQualifiedName service, msg_in, msg_out;
			//public string wsdl, port, op;
			public string xsd;
			public XmlQualifiedName elem;
			public string Serialize()
			{
				using (var stream = new MemoryStream())
				{
					using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.Default))
						new BinaryFormatter().Serialize(zstream, this);
					return Convert.ToBase64String(stream.ToArray());
				}
			}
			public static Element Deserialize(string s)
			{
				using (var stream = new MemoryStream(Convert.FromBase64String(s), false))
				using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress))
					return (Element)new BinaryFormatter().Deserialize(zstream);
			}
			//public string ServiceText { get { return "Service: " + service.Name; } }
			//public string PortText { get { return "Port: " + port; } }
			//public string OpText { get { return /*"Operation: " +*/ op /*+ (doc != null ? "\t-\t" + doc.InnerText : "")*/; } }
			//public string TreeViewPath { get { return ServiceText + "/" + PortText + "/" + OpText; } }
			public string SchemaText { get { return "Schema: " + xsd; } }
			public string ElemText { get { return "Element: " + elem; } }
			public string TreeViewPath { get { return SchemaText + "\\" + ElemText; } }
		}

		private static void LoadXsd(string url, IDictionary<string, XmlDocument> xsd_, ref XmlNamespaceManager nsmgr)
		{
			var res = new XmlDocument();
			res.Load(url);
			xsd_.Add(url, res);
			if (nsmgr == null)
			{
				nsmgr = new XmlNamespaceManager(res.NameTable);
				nsmgr.AddNamespace("xml", "http://www.w3.org/XML/1998/namespace");
				nsmgr.AddNamespace("s", "http://www.w3.org/2001/XMLSchema");
				//nsmgr.AddNamespace("w", "http://schemas.xmlsoap.org/wsdl/");
				//nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");
				//nsmgr.AddNamespace("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
				//nsmgr.AddNamespace("http", "http://schemas.xmlsoap.org/wsdl/http/");
			}
			foreach (XmlNode import in res.SelectNodes("/s:schema/s:import", nsmgr))
				if (import.Attributes["location"] != null && "" + import.Attributes["location"].Value != "")
					LoadXsd(import.Attributes["location"].Value, xsd_, ref nsmgr);
		}
		protected static IDictionary<string, XmlDocument> LoadXsd(string url, out XmlNamespaceManager nsmgr)
		{
			var res = new Dictionary<string, XmlDocument>();
			XmlNamespaceManager nsmgr_ = null;
			LoadXsd(url, res, ref nsmgr_);
			nsmgr = nsmgr_;
			return res;
		}

		private class XmlCachingResolver : XmlUrlResolver
		{
			public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
			{
				var path = System.Web.Hosting.HostingEnvironment.MapPath(
					"~/App_Data/" + absoluteUri.Host + absoluteUri.LocalPath);
				if (!File.Exists(path))
					using (var instr = new StreamReader((Stream)base.GetEntity(absoluteUri, role, ofObjectToReturn)))
					{
						Directory.CreateDirectory(Path.GetDirectoryName(path));
						using (var file = new StreamWriter(path))
							file.Write(instr.ReadToEnd());
					}
				return File.OpenRead(path);
			}
		}
		protected static XmlSchemaSet LoadCompileSchemas(IEnumerable<XmlDocument> xsd_, XmlNamespaceManager nsmgr)
		{
			XmlSchemaSet xss = new XmlSchemaSet(xsd_.First().NameTable);
			foreach (var xn in xsd_.Select(x => x.DocumentElement))
				xss.Add(XmlSchema.Read(new XmlNodeReader(xn), null));
			try
			{
				xss.Compile();
			}
			catch
			{
				var xml_reader_settings = new XmlReaderSettings()
				{
					ProhibitDtd = false,
					XmlResolver = new XmlCachingResolver()
				};

				xss.Add("http://www.w3.org/XML/1998/namespace",
					XmlReader.Create("http://www.w3.org/2001/xml.xsd", xml_reader_settings));

				xss.Add("http://www.w3.org/2001/XMLSchema",
					XmlReader.Create("http://www.w3.org/2001/XMLSchema.xsd", xml_reader_settings));

				xss.Compile();
			}
			return xss;
		}

		protected static void AddXsdUrlToJsonAutocompleteStorage(string storagePath, string xsdUrl)
		{
			if ("" + xsdUrl == "")
				return;
			var ser = new DataContractJsonSerializer(typeof(XsdList));
			if (!File.Exists(storagePath))
				using (var writer = File.CreateText(storagePath))
					ser.WriteObject(writer.BaseStream, new XsdList()
					{
						urls = new[] { xsdUrl }
					});
			else
			{
				XsdList obj;
				using (var reader = File.OpenText(storagePath))
					obj = (XsdList)ser.ReadObject(reader.BaseStream);
				if (!obj.urls.Any(s => xsdUrl.Equals(s, StringComparison.OrdinalIgnoreCase)))
				{
					var urls = new List<string>(obj.urls);
					urls.Add(xsdUrl);
					urls.Sort(StringComparer.OrdinalIgnoreCase);
					obj.urls = urls.ToArray();
					using (var writer = new StreamWriter(storagePath, false))
						ser.WriteObject(writer.BaseStream, obj);
				}
			}
		}

		protected static void BuildServiceTreeView(TreeView TreeView1, IDictionary<string, XmlDocument> xsd_, XmlNamespaceManager nsmgr)
		{
			TreeView1.Nodes.Clear();
			TreeView1.PathSeparator = '\\';
			foreach (var schema in xsd_)
			{
				var element = new Element() { xsd = schema.Key };
				var schema_node = new TreeNode(element.SchemaText);
				schema_node.SelectAction = TreeNodeSelectAction.Expand;
				TreeView1.Nodes.Add(schema_node);
				foreach (var elem in schema.Value.DocumentElement.SelectNodes("s:element", nsmgr).Cast<XmlElement>())
				{
					element.elem = elem.GetName();
					var element_node = new TreeNode(element.ElemText, null, null, "Elem.aspx?element=" + HttpUtility.UrlEncode(element.Serialize()), /*"_blank"*/null);
					schema_node.ChildNodes.Add(element_node);
				}
			}
		}
	}

	[DataContract]
	public class XsdList
	{
		[DataMember]
		public string[] urls;
	}
}
