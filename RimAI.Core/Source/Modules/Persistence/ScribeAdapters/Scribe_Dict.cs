using System.Collections.Generic;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.ScribeAdapters
{
	public static class Scribe_Dict
	{
		public static void Look(ref Dictionary<string, List<string>> dict, string label)
		{
			Scribe_Collections.Look(ref dict, label, LookMode.Value, LookMode.Value);
			dict ??= new Dictionary<string, List<string>>();
		}

		public static void Look(ref Dictionary<string, string> dict, string label)
		{
			Scribe_Collections.Look(ref dict, label, LookMode.Value, LookMode.Value);
			dict ??= new Dictionary<string, string>();
		}

		public static void Look<T>(ref Dictionary<string, List<T>> dict, string label)
		{
			Scribe_Collections.Look(ref dict, label, LookMode.Value, LookMode.Deep);
			dict ??= new Dictionary<string, List<T>>();
		}
	}
}


