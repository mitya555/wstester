using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace wstester
{
	using System.IO;
	using System.Text;
	using System.Xml;

	public static class Soap
	{
		public static XmlNode CreateDocGetEnvelope(bool soap11, out XmlDocument doc)
		{
			var envelopeNamespace = soap11 ?
				"http://schemas.xmlsoap.org/soap/envelope/" :
				"http://www.w3.org/2003/05/soap-envelope";
			doc = new XmlDocument();
			doc.AppendChild(doc.CreateXmlDeclaration("1.0", null, null));
			return doc
				.AppendChild(doc.CreateElement("Envelope", envelopeNamespace))
				.AppendChild(doc.CreateElement("Body", envelopeNamespace));
		}

		public static void GetXml(this XmlDocument doc, TextWriter outwriter, bool formatted)
		{
			using (var xmlTextWriter = new XmlTextWriter(outwriter) { Formatting = formatted ? Formatting.Indented : Formatting.None })
				doc.Save(xmlTextWriter);
		}

		public static string GetXml(this XmlDocument doc, bool formatted)
		{
			using (var stringWriter = new Utf8StringWriter())
			{
				GetXml(doc, stringWriter, formatted);
				return stringWriter.ToString();
			}
		}
	}

	public sealed class Utf8StringWriter : StringWriter
	{
		public override Encoding Encoding { get { return Encoding.UTF8; } }
	}
}