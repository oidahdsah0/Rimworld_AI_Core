using System.Collections.Generic;
using Newtonsoft.Json;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.ScribeAdapters
{
	public static class Scribe_Poco
	{
		public static void LookJsonDict<T>(ref Dictionary<string, T> dict, string label)
		{
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var tmp = new Dictionary<string, string>();
				if (dict != null)
				{
					foreach (var kv in dict)
					{
						var json = JsonConvert.SerializeObject(kv.Value);
						if (json != null && json.Length > 32760)
						{
							// RimWorld 字符串节点存在长度限制，必要时局部截断
							json = json.Substring(0, 32760);
						}
						tmp[kv.Key] = json;
					}
				}
				Scribe_Collections.Look(ref tmp, label, LookMode.Value, LookMode.Value);
			}
			else
			{
				Dictionary<string, string> tmp = null;
				Scribe_Collections.Look(ref tmp, label, LookMode.Value, LookMode.Value);
				dict = new Dictionary<string, T>();
				if (tmp != null)
				{
					foreach (var kv in tmp)
					{
						try { dict[kv.Key] = JsonConvert.DeserializeObject<T>(kv.Value); }
						catch { dict[kv.Key] = default(T); }
					}
				}
			}
		}

		public static void LookJsonList<T>(ref List<T> list, string label)
		{
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var tmp = new List<string>();
				if (list != null)
				{
					foreach (var v in list)
					{
						var json = JsonConvert.SerializeObject(v);
						if (json != null && json.Length > 32760) json = json.Substring(0, 32760);
						tmp.Add(json);
					}
				}
				Scribe_Collections.Look(ref tmp, label, LookMode.Value);
			}
			else
			{
				List<string> tmp = null;
				Scribe_Collections.Look(ref tmp, label, LookMode.Value);
				list = new List<T>();
				if (tmp != null)
				{
					foreach (var s in tmp)
					{
						try { list.Add(JsonConvert.DeserializeObject<T>(s)); } catch { list.Add(default(T)); }
					}
				}
			}
		}
	}
}


