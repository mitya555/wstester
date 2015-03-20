<%@ WebHandler Language="C#" CodeBehind="Urls.ashx.cs" Class="wstester.Urls" %>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Web.Script.Serialization;

namespace wstester
{
	public class Urls : IHttpHandler
	{

		public void ProcessRequest(HttpContext context)
		{
			context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			context.Response.Cache.SetNoStore();
			context.Response.ContentType = "text/plain";
			var filename = "WsdlList.json";
			if (context.Request["filename"] != null)
				filename = context.Request["filename"];
			if (!File.Exists(context.Server.MapPath("~/App_Data/" + filename)))
			{
				context.Response.End();
				return;
			}
			WsdlList obj;
			var ser = new DataContractJsonSerializer(typeof(WsdlList));
			using (var reader = File.OpenText(context.Server.MapPath("~/App_Data/" + filename)))
				obj = (WsdlList)ser.ReadObject(reader.BaseStream);
			obj.urls = obj.urls
				.Where(s => s.IndexOf(context.Request["term"], StringComparison.OrdinalIgnoreCase) > -1)
				.ToArray();
			//ser.WriteObject(context.Response.OutputStream, obj);
			context.Response.Write(new JavaScriptSerializer().Serialize(obj.urls));
		}

		public bool IsReusable
		{
			get
			{
				return false;
			}
		}
	}
}
