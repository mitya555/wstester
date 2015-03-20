using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

using System.Diagnostics;
using System.Resources;
using System.Reflection;
using CassiniDev;

namespace winwstester
{
	static class Program
	{
		static void Main()
		{
			var server = new Server(1234, Environment.CurrentDirectory); // CassiniDevServer();

			var assembly = Assembly.GetExecutingAssembly();

			var icon = new NotifyIcon()
			{
				Icon = new Icon(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
					.First(s => s.EndsWith("Icon1.ico"))), 16, 16), // new Icon("Icon1.ico", 16, 16),
				Visible = true
			};

			icon.ContextMenu = new ContextMenu(new[]
			{
				new MenuItem("&Open in Browser", (o, a) => StartBrowser("http://localhost:1234/wsdl.aspx")),
				new MenuItem("-"),
				new MenuItem("&Quit", (o, a) =>
				{
					server.ShutDown();
					icon.Visible = false;
					Application.Exit();
				})
			});

			server.Start(); // StartServer(Path.Combine(Environment.CurrentDirectory, @"..\..\..\wstester"));

			StartBrowser("http://localhost:1234/wsdl.aspx");

			Application.Run();
		}

		static void StartBrowser(string url)
		{
			new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					WindowStyle = ProcessWindowStyle.Hidden,
					FileName = "cmd.exe",
					Arguments = "/C start " + url
				}
			}.Start();
		}
	}
}
