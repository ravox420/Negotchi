using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal static class Json
    {
        public static string Encode(object value) { return new JavaScriptSerializer { MaxJsonLength = 16777216 }.Serialize(value); }
        public static Dictionary<string, object> Decode(string text) { return new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<Dictionary<string, object>>(text); }
        public static Dictionary<string, object> Map(params object[] values)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i += 2) result.Add((string)values[i], values[i + 1]);
            return result;
        }
        public static object Get(IDictionary<string, object> value, string key) { object item; return value != null && value.TryGetValue(key, out item) ? item : null; }
        public static Dictionary<string, object> Object(IDictionary<string, object> value, string key) { return Get(value, key) as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        public static string Text(IDictionary<string, object> value, string key) { return Convert.ToString(Get(value, key)); }
        public static object[] Items(IDictionary<string, object> value, string key)
        {
            var items = Get(value, key) as IEnumerable;
            return items == null || items is string ? new object[0] : items.Cast<object>().ToArray();
        }
    }
}
