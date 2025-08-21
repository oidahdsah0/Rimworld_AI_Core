using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Server
{
	internal interface IServerPromptPresetManager
	{
		Task<ServerPromptPreset> GetAsync(string locale, CancellationToken ct = default);
	}

	internal sealed class ServerPromptPreset
	{
		public int Version { get; set; }
		public string Locale { get; set; }
		public string Base { get; set; }
		public EnvSection Env { get; set; } = new EnvSection();
		public IReadOnlyList<BasePersonaOption> BaseOptions { get; set; }

		internal sealed class EnvSection
		{
			public string temp_low { get; set; }
			public string temp_mid { get; set; }
			public string temp_high { get; set; }
		}

		internal sealed class BasePersonaOption
		{
			public string key { get; set; }
			public string title { get; set; }
			public string text { get; set; }
		}
	}
}


