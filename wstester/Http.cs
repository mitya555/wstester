using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace wstester
{
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Net;
	using System.Net.Sockets;
	using System.Xml;

	public static class Http
	{
		public class Header
		{
			public string Name { get; set; }
			public string Value { get; set; }
		}

		public static string BuildRequest(Uri url, string body, IEnumerable<Header> headers)
		{
			var headers_ = "";
			bool bHttpWebRequest = (url.Scheme == Uri.UriSchemeHttps);
//#if HttpWebRequest
			if (bHttpWebRequest)
			{
				var req = CreateRequest(url, body, headers);
				foreach (string header in req.Headers)
					headers_ += header + ": " + req.Headers[header] + "\r\n";
			}
//#else
			else
			{
				headers_ = headers.Aggregate("", (s, h) => s + h.Name + ": " + h.Value + "\r\n");
			}
//#endif
			return "POST " + url.PathAndQuery + " HTTP/1.1\r\n" + headers_ + "\r\n" + body;
		}

//#if HttpWebRequest
	
		public static HttpWebRequest CreateRequest(Uri url, string body, IEnumerable<Header> headers)
		{
			var req = (HttpWebRequest)HttpWebRequest.Create(url);
			req.Method = "POST";
			req.ProtocolVersion = new Version("1.1");
			req.Headers.Clear();
			foreach (var header in headers)
				try { req.Headers.Add(header.Name, header.Value); }
				catch (Exception ex)
				{
					if (ex.Message.StartsWith("This header must be modified using the appropriate property"))
						switch (header.Name)
						{
							case "Content-Type": req.ContentType = header.Value; break;
							case "Content-Length": req.ContentLength = long.Parse(header.Value); break;
						}
				}
			using (var instr = req.GetRequestStream())
			using (var inwriter = new StreamWriter(instr))
				inwriter.Write(body);
			return req;
		}

		public static string GetResponseXml(HttpWebRequest req, bool formatResultXml)
		{
			using (var res = (HttpWebResponse)req.GetResponse())
				return FormatResponseXml(res, formatResultXml);
		}

		public static string FormatResponseXml(HttpWebResponse res, bool formatResultXml)
		{
			using (var outwriter = new Utf8StringWriter())
			{
				// Response Headers
				outwriter.WriteLine("HTTP/" + res.ProtocolVersion + " " + (int)res.StatusCode + " " + res.StatusDescription);
				foreach (string header in res.Headers)
					outwriter.WriteLine(header + ": " + res.Headers[header]);
				outwriter.WriteLine();
				// Response Body
				if (formatResultXml)
				{
					var xmlDoc = new XmlDocument();
					using (var outstr = res.GetResponseStream())
						xmlDoc.Load(outstr);
					xmlDoc.GetXml(outwriter, true);
				}
				else
				{
					using (var outstr = res.GetResponseStream())
					using (var outreader = new StreamReader(outstr))
						outwriter.Write(outreader.ReadToEnd());
				}
				// Result to string
				return outwriter.ToString();
			}
		}
	
//#else

		private static Socket ConnectSocket(string hostname, int port)
		{
			Socket s = null;
			// Get host related information.
			IPHostEntry hostEntry;
			if (!Regex.IsMatch(hostname, @"^(\d{1,3}\.){3}\d{1,3}$"))
			{
				hostEntry = Dns.GetHostEntry(hostname);
			}
			else
			{
				hostEntry = new IPHostEntry() { HostName = hostname, AddressList = new[] { new IPAddress(hostname.Split('.').Select(b => byte.Parse(b)).ToArray()) } };
			}
			// Loop through the AddressList to obtain the supported AddressFamily. This is to avoid 
			// an exception that occurs when the host IP Address is not compatible with the address family 
			// (typical in the IPv6 case). 
			foreach (IPAddress address in hostEntry.AddressList)
			{
				var ipe = new IPEndPoint(address, port);
				var tempSocket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				tempSocket.Connect(ipe);
				if (tempSocket.Connected)
				{
					s = tempSocket;
					break;
				}
				else
					continue;
			}
			return s;
		}

		enum ParseStatus
		{
			reading_header,
			reading_non_chunked,
			reading_chunk_length,
			reading_chunk_extension,
			reading_chunk_data,
			reading_trailer
		}

		static readonly byte[] HEX_CHARS =
		{
			Convert.ToByte('0'),
			Convert.ToByte('1'),
			Convert.ToByte('2'),
			Convert.ToByte('3'),
			Convert.ToByte('4'),
			Convert.ToByte('5'),
			Convert.ToByte('6'),
			Convert.ToByte('7'),
			Convert.ToByte('8'),
			Convert.ToByte('9'),
			Convert.ToByte('A'),
			Convert.ToByte('B'),
			Convert.ToByte('C'),
			Convert.ToByte('D'),
			Convert.ToByte('E'),
			Convert.ToByte('F'),
			Convert.ToByte('a'),
			Convert.ToByte('b'),
			Convert.ToByte('c'),
			Convert.ToByte('d'),
			Convert.ToByte('e'),
			Convert.ToByte('f')
		};

		public static string SocketSendReceive(string hostname, int port, string httpPayload)
		{
			var bytesSent = Encoding.UTF8.GetBytes(httpPayload);
			const int BUFLEN = 8192;
			var bytesReceived = new Byte[BUFLEN];
			// Create a socket connection with the specified server and port.
			var socket = ConnectSocket(hostname, port);
			if (socket == null)
				return ("Connection failed");
			// Send request to the server.
			socket.Send(bytesSent, bytesSent.Length, 0);
			// Receive the server content. 
			string result = "", header = "";
			// The following will block until the result is transmitted.
			using (var stream = new MemoryStream())
			{
				var buf = stream.GetBuffer();
				int bytes = 0, total_bytes = 0, cont_len = int.MaxValue,
					start_pos = 0, pos = 0, matchedCrLf = 0;
				ParseStatus parse_status = ParseStatus.reading_header;
				do
				{
					total_bytes += (bytes = socket.Receive(bytesReceived, BUFLEN, 0));
					stream.Write(bytesReceived, 0, bytes);
					buf = stream.GetBuffer();

					while (parse_status == ParseStatus.reading_header && pos < total_bytes)
					{
						switch (matchedCrLf)
						{
							case 0: matchedCrLf = buf[pos] == 13 ? 1 : 0; break;
							case 1: matchedCrLf = buf[pos] == 10 ? 2 : buf[pos] == 13 ? 1 : 0; break;
							case 2: matchedCrLf = buf[pos] == 13 ? 3 : 0; break;
							case 3: matchedCrLf = buf[pos] == 10 ? 4 : buf[pos] == 13 ? 1 : 0; break;
						}
						if (matchedCrLf == 4)
						{
							var head_len = pos - 3;
							var header_ = Encoding.UTF8.GetString(buf, 0, head_len);
							var header_str_arr = Regex.Split(header_, @"\r\n").Skip(1);
							var headers = header_str_arr.Select(s =>
							{
								var tmp = s.Split(new[] { ':' }, 2);
								return new Header()
								{
									Name = tmp[0].Trim(),
									Value = tmp.Length == 2 ? tmp[1].Trim() : null
								};
							});
							var cont_len_header = headers.FirstOrDefault(h =>
								"Content-Length".Equals(h.Name, StringComparison.OrdinalIgnoreCase));
							if (cont_len_header != null)
							{
								cont_len = int.Parse(cont_len_header.Value);
								parse_status = ParseStatus.reading_non_chunked;
							}
							else if (headers.Any(h =>
									"Transfer-Encoding".Equals(h.Name, StringComparison.OrdinalIgnoreCase) &&
									"chunked".Equals(h.Value, StringComparison.OrdinalIgnoreCase)))
							{
								header = header_;
								parse_status = ParseStatus.reading_chunk_length;
								start_pos = pos + 1;
							}
							else
								parse_status = ParseStatus.reading_non_chunked;
							matchedCrLf = 0;
						}
						pos++;
					}

					while (parse_status == ParseStatus.reading_chunk_length && pos < total_bytes)
					{
						if (Array.IndexOf(HEX_CHARS, buf[pos]) == -1)
						{
							cont_len = int.Parse(Encoding.UTF8.GetString(buf, start_pos, pos - start_pos),
								System.Globalization.NumberStyles.HexNumber);
							parse_status = ParseStatus.reading_chunk_extension;
						}
						else
							pos++;
					}
					while (parse_status == ParseStatus.reading_chunk_extension && pos < total_bytes)
					{
						switch (matchedCrLf)
						{
							case 0: matchedCrLf = buf[pos] == 13 ? 1 : 0; break;
							case 1: matchedCrLf = buf[pos] == 10 ? 2 : buf[pos] == 13 ? 1 : 0; break;
						}
						if (matchedCrLf == 2)
						{
							if (cont_len > 0)
								parse_status = ParseStatus.reading_chunk_data;
							else
							{
								parse_status = ParseStatus.reading_trailer;
								pos -= 2;
								start_pos = pos + 1;
							}
							matchedCrLf = 0;
						}
						pos++;
					}
					if (parse_status == ParseStatus.reading_chunk_data && pos + cont_len <= total_bytes)
					{
						result += Encoding.UTF8.GetString(buf, pos, cont_len);
						start_pos = (pos += cont_len + 2); // past CrLf
						parse_status = ParseStatus.reading_chunk_length;
					}

					while (parse_status == ParseStatus.reading_trailer && pos < total_bytes)
					{
						switch (matchedCrLf)
						{
							case 0: matchedCrLf = buf[pos] == 13 ? 1 : 0; break;
							case 1: matchedCrLf = buf[pos] == 10 ? 2 : buf[pos] == 13 ? 1 : 0; break;
							case 2: matchedCrLf = buf[pos] == 13 ? 3 : 0; break;
							case 3: matchedCrLf = buf[pos] == 10 ? 4 : buf[pos] == 13 ? 1 : 0; break;
						}
						if (matchedCrLf == 4)
						{
							var trail_end = pos - 3;
							if (trail_end > start_pos)
								header += Encoding.UTF8.GetString(buf, start_pos, trail_end - start_pos);
							return header + "\r\n\r\n" + result;
						}
						pos++;
					}

					if (parse_status == ParseStatus.reading_non_chunked && cont_len <= total_bytes - pos)
						break;
				}
				while (bytes > 0);

				return Encoding.UTF8.GetString(buf, 0, total_bytes);
			}
		}

		public static string FormatSocketXml(string result)
		{
			var pos = result.IndexOf("\r\n\r\n") + 4;
			var xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(result.Substring(pos));
			return result.Substring(0, pos) + xmlDoc.GetXml(true);
		}

//#endif

	}
}