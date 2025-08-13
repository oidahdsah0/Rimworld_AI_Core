using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RimAI.Core.Source.Modules.Tooling
{
	internal static class ToolDiscovery
	{
		public static List<IRimAITool> DiscoverTools()
		{
			var list = new List<IRimAITool>();
			var asm = Assembly.GetExecutingAssembly();
			foreach (var t in asm.GetTypes())
			{
				if (!typeof(IRimAITool).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface) continue;
				try
				{
					if (Activator.CreateInstance(t) is IRimAITool tool)
					{
						list.Add(tool);
					}
				}
				catch { }
			}
			return list.OrderBy(t => t.Name).ToList();
		}
	}
}



