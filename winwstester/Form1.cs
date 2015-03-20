using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using CassiniDev;
using System.IO;

namespace winwstester
{
	public partial class Form1 : Form
	{

		private readonly /*CassiniDevServer*/ Server server;

		public Form1()
		{
			InitializeComponent();

			server = new Server(/*Path.Combine(*/Environment.CurrentDirectory/*, @"..\..\..\wstester")*/); // CassiniDevServer();

			// our content is Copy Always into bin
			server.Start(); // StartServer(Path.Combine(Environment.CurrentDirectory, @"..\..\..\wstester"));

			webBrowser1.Navigate(CassiniNetworkUtils.NormalizeUrl(server.RootUrl, "Wsdl.aspx")); // server.NormalizeUrl("Wsdl.aspx"));
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			server.ShutDown(); // StopServer();
		}
	}
}
