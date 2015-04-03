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

	using System.Net;
	using System.Text;

	public partial class Wsdl : System.Web.UI.Page
	{
		[Serializable]
		public class Operation
		{
			public XmlQualifiedName service, msg_in, msg_out;
			public string wsdl, port, op;
			public string username, password;
			public string Serialize()
			{
				using (var stream = new MemoryStream())
				{
					using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.Default))
						new BinaryFormatter().Serialize(zstream, this);
					return Convert.ToBase64String(stream.ToArray());
				}
			}
			public static Operation Deserialize(string s)
			{
				using (var stream = new MemoryStream(Convert.FromBase64String(s), false))
				using (var zstream = new Ionic.Zlib.ZlibStream(stream, Ionic.Zlib.CompressionMode.Decompress))
					return (Operation)new BinaryFormatter().Deserialize(zstream);
			}
			public string ServiceText { get { return "Service: " + service.Name; } }
			public string PortText { get { return "Port: " + port; } }
			public string OpText { get { return /*"Operation: " +*/ op /*+ (doc != null ? "\t-\t" + doc.InnerText : "")*/; } }
			public string TreeViewPath { get { return ServiceText + "/" + PortText + "/" + OpText; } }
		}

		private static void LoadWsdl(string url, IList<XmlDocument> wsdl_, ref XmlNamespaceManager nsmgr, string username, string password)
		{
			var res = new XmlDocument();
			//if ("" + username == "")
			//	res.Load(url);
			//else
			//{
			//	var req = WebRequest.Create(url);
			//	byte[] credentialBuffer = new UTF8Encoding().GetBytes(username + ":" + password);
			//	req.Headers["Authorization"] = "Basic " + Convert.ToBase64String(credentialBuffer);
			//	using (var resp = req.GetResponse())
			//	using (var stream = resp.GetResponseStream())
			//		res.Load(stream);
			//}
			if ("" + username != "")
				res.XmlResolver = new XmlUrlResolver() { Credentials = new NetworkCredential(username, password) };
			res.Load(url);
			wsdl_.Add(res);
			if (nsmgr == null)
			{
				nsmgr = new XmlNamespaceManager(res.NameTable);
				nsmgr.AddNamespace("xml", "http://www.w3.org/XML/1998/namespace");
				nsmgr.AddNamespace("s", "http://www.w3.org/2001/XMLSchema");
				nsmgr.AddNamespace("w", "http://schemas.xmlsoap.org/wsdl/");
				nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");
				nsmgr.AddNamespace("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
				nsmgr.AddNamespace("http", "http://schemas.xmlsoap.org/wsdl/http/");
			}
			foreach (XmlNode import in res.SelectNodes("/w:definitions/w:import", nsmgr))
				if (import.Attributes["location"] != null && "" + import.Attributes["location"].Value != "")
					LoadWsdl(import.Attributes["location"].Value, wsdl_, ref nsmgr, username, password);
		}
		protected static XmlDocument[] LoadWsdl(string url, out XmlNamespaceManager nsmgr, string username, string password)
		{
			var res = new List<XmlDocument>();
			XmlNamespaceManager nsmgr_ = null;
			LoadWsdl(url, res, ref nsmgr_, username, password);
			nsmgr = nsmgr_;
			return res.ToArray();
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
		protected static XmlSchemaSet LoadCompileSchemas(IEnumerable<XmlDocument> wsdl_, XmlNamespaceManager nsmgr, string username, string password)
		{
			XmlSchemaSet xss = new XmlSchemaSet(wsdl_.First().NameTable);
			xss.ValidationEventHandler += xss_ValidationEventHandler;
			if ("" + username != "")
				xss.XmlResolver = new XmlUrlResolver() { Credentials = new NetworkCredential(username, password) };
			foreach (var xn in wsdl_.SelectMany(x => x.SelectNodes("/w:definitions/w:types/s:schema", nsmgr).Cast<XmlNode>()))
				xss.Add(XmlSchema.Read(new XmlNodeReader(xn), null));
			xss.ValidationEventHandler -= xss_ValidationEventHandler;
			try
			{
				xss.Compile();
			}
			catch (Exception ex)
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

		static void xss_ValidationEventHandler(object sender, ValidationEventArgs e)
		{
			//throw new NotImplementedException();
			if (e.Exception != null)
				throw e.Exception;
		}

		protected static void AddWsdlUrlToJsonAutocompleteStorage(string storagePath, string wsdlUrl)
		{
			if ("" + wsdlUrl == "")
				return;
			var ser = new DataContractJsonSerializer(typeof(WsdlList));
			if (!File.Exists(storagePath))
				using (var writer = File.CreateText(storagePath))
					ser.WriteObject(writer.BaseStream, new WsdlList()
					{
						urls = new[] { wsdlUrl }
					});
			else
			{
				WsdlList obj;
				using (var reader = File.OpenText(storagePath))
					obj = (WsdlList)ser.ReadObject(reader.BaseStream);
				if (!obj.urls.Any(s => wsdlUrl.Equals(s, StringComparison.OrdinalIgnoreCase)))
				{
					var urls = new List<string>(obj.urls);
					urls.Add(wsdlUrl);
					urls.Sort(StringComparer.OrdinalIgnoreCase);
					obj.urls = urls.ToArray();
					using (var writer = new StreamWriter(storagePath, false))
						ser.WriteObject(writer.BaseStream, obj);
				}
			}
		}

		protected static void BuildServiceTreeView(TreeView TreeView1, string wsdlUrl, IList<XmlDocument> wsdl_, XmlNamespaceManager nsmgr, string username, string password)
		{
			TreeView1.Nodes.Clear();
			var operation = new Wsdl.Operation() { wsdl = wsdlUrl, username = username, password = password };
			foreach (var wservice in wsdl_.SelectMany(x => x.SelectNodes("/w:definitions/w:service", nsmgr).Cast<XmlElement>()))
			{
				operation.service = wservice.GetName();
				var service_node = new TreeNode(operation.ServiceText);
				service_node.SelectAction = TreeNodeSelectAction.Expand;
				TreeView1.Nodes.Add(service_node);
				foreach (var port in wservice.SelectNodes("w:port", nsmgr).Cast<XmlElement>())
				{
					operation.port = port.Attributes["name"].Value;
					var port_node = new TreeNode(operation.PortText);
					port_node.SelectAction = TreeNodeSelectAction.Expand;
					service_node.ChildNodes.Add(port_node);
					var binding = port.GetBindingFromPort(wsdl_, nsmgr);
					var port_type = binding.GetPortTypeFromBinding(wsdl_, nsmgr);
					foreach (var op in port_type.SelectNodes("w:operation", nsmgr).Cast<XmlElement>().OrderBy(o => o.Attributes["name"].Value))
					{
						var doc = op.SelectSingleNode("w:documentation", nsmgr);
						operation.op = op.Attributes["name"].Value;
						var op_node = new TreeNode(operation.OpText);
						port_node.ChildNodes.Add(op_node);
						if (doc != null)
						{
							var doc_node = new TreeNode(doc.InnerText);
							doc_node.SelectAction = TreeNodeSelectAction.Expand;
							op_node.ChildNodes.Add(doc_node);
						}
						operation.msg_in = op.GetInputOutputFromOperation(InOut.input, nsmgr).GetMessageFromInputOutput(wsdl_, nsmgr).GetName();
						operation.msg_out = op.GetInputOutputFromOperation(InOut.output, nsmgr).GetMessageFromInputOutput(wsdl_, nsmgr).GetName();
						op_node.NavigateUrl = "Op.aspx?operation=" + HttpUtility.UrlEncode(operation.Serialize());
						//op_node.Target = "_blank";
					}
				}
			}
		}
	}

	[DataContract]
	public class WsdlList
	{
		[DataMember]
		public string[] urls;
	}

	public static partial class Extensions
	{

		public static XmlNode GetServiceByName(this IList<XmlDocument> wsdl_, XmlQualifiedName name, XmlNamespaceManager nsmgr)
		{
			return wsdl_.SelectSingleNodeByNameGlobal("/w:definitions/w:service", name, nsmgr);
		}

		public static XmlNode GetPortFromServiceByName(this XmlNode service, string name, XmlNamespaceManager nsmgr)
		{
			return service.SelectSingleNodeByValue("w:port[@name='{0}']", name, nsmgr);
		}

		public static XmlNode GetBindingFromPort(this XmlNode port, IList<XmlDocument> wsdl_, XmlNamespaceManager nsmgr)
		{
			return port.SelectSingleNodeByNameFromAttrGlobal("/w:definitions/w:binding", "binding", wsdl_, nsmgr);
		}

		public static XmlNode GetPortTypeFromBinding(this XmlNode binding, IList<XmlDocument> wsdl_, XmlNamespaceManager nsmgr)
		{
			return binding.SelectSingleNodeByNameFromAttrGlobal("/w:definitions/w:portType", "type", wsdl_, nsmgr);
		}

		public static XmlNode GetOperationFromParentByName(this XmlNode parent, string name, XmlNamespaceManager nsmgr)
		{
			return parent.SelectSingleNodeByValue("w:operation[@name='{0}']", name, nsmgr);
		}

		public static XmlNode GetInputOutputFromOperation(this XmlNode operation, InOut inOut, XmlNamespaceManager nsmgr)
		{
			return operation.SelectSingleNode("w:" + inOut, nsmgr);
		}

		public static XmlNode GetMessageFromInputOutput(this XmlNode inOut, IList<XmlDocument> wsdl_, XmlNamespaceManager nsmgr)
		{
			return inOut.SelectSingleNodeByNameFromAttrGlobal("/w:definitions/w:message", "message", wsdl_, nsmgr);
		}
	}
}
