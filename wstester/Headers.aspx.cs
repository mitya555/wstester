using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.ComponentModel;

namespace wstester
{
	[DataObject]
	public partial class Headers : System.Web.UI.Page
	{
		public class HttpHeader : Http.Header
		{
			public Guid Id { get; set; }
			public long Changed { get; set; } // in milliseconds since 1 January 1970 00:00:00 UTC
		}
		public static IDictionary<Guid, HttpHeader> Dict
		{
			get
			{
				if (HttpContext.Current.Session["headers"] == null)
					HttpContext.Current.Session["headers"] = new Dictionary<Guid, HttpHeader>();
				return (IDictionary<Guid, HttpHeader>)HttpContext.Current.Session["headers"];
			}
		}
		[DataObjectMethod(DataObjectMethodType.Select, true)]
		public static IEnumerable<HttpHeader> Select()
		{
			return Dict.Values.Concat(new[] { new HttpHeader() });
		}
		[DataObjectMethod(DataObjectMethodType.Insert, true)]
		public static void Insert(HttpHeader header)
		{
			var new_key = Guid.NewGuid();
			header.Id = new_key;
			Dict.Add(new_key, header);
		}
		[DataObjectMethod(DataObjectMethodType.Update, true)]
		public static void Update(HttpHeader header)
		{
			if (header.Id.Equals(Activator.CreateInstance(typeof(Guid))))
				Insert(header);
			else
				Dict[header.Id] = header;
		}
		[DataObjectMethod(DataObjectMethodType.Delete, true)]
		public static void Delete(HttpHeader header)
		{
			Dict.Remove(header.Id);
		}

		protected void GridView1_RowDataBound(object sender, GridViewRowEventArgs e)
		{
			//if (e.Row.RowType == DataControlRowType.DataRow && ((Header)e.Row.DataItem).Id.Equals(Activator.CreateInstance(typeof(Guid))))
			//    e.Row.Visible = false;
		}
	}
}
