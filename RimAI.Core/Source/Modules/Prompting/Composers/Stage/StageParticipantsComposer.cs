using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Stage
{
	internal sealed class StageParticipantsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.Stage;
		public int Order => 40;
		public string Id => "stage_participants";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var participants = ctx?.Request?.ParticipantIds?.Where(id => id != null && id.StartsWith("pawn:"))?.ToList() ?? new List<string>();
				if (participants.Count == 0) return new ComposerOutput { SystemLines = Array.Empty<string>(), ContextBlocks = Array.Empty<ContextBlock>() };

				// 针对每个参与者获取快照与补充信息（并发）
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var personaSvc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Persona.IPersonaService>();
				var tasks = new List<Task<(int id, RimAI.Core.Source.Modules.World.PawnPromptSnapshot prompt, RimAI.Core.Source.Modules.World.NeedsSnapshot needs, RimAI.Core.Source.Modules.World.PawnHealthSnapshot health, string job)>>();
				foreach (var id in participants)
				{
					if (!int.TryParse(id.Substring(5), out var pid)) continue;
					var t = Task.Run(async () =>
					{
						var prompt = await world.GetPawnPromptSnapshotAsync(pid, ct).ConfigureAwait(false);
						var needs = await world.GetNeedsAsync(pid, ct).ConfigureAwait(false);
						var health = await world.GetPawnHealthSnapshotAsync(pid, ct).ConfigureAwait(false);
						var job = await world.GetCurrentJobLabelAsync(pid, ct).ConfigureAwait(false);
						return (pid, prompt, needs, health, job);
					}, ct);
					tasks.Add(t);
				}
				var results = await Task.WhenAll(tasks).ConfigureAwait(false);

				int index = 0;
				foreach (var r in results)
				{
					index++;
					var name = r.prompt?.Id?.Name ?? ($"pawn:{r.id}");
					var gender = r.prompt?.Id?.Gender ?? string.Empty;
					var age = r.prompt?.Id?.Age > 0 ? r.prompt.Id.Age.ToString() : string.Empty;
					var title = r.prompt?.Story != null ? (ctx?.L?.Invoke("prompt.section.job", "[职务]") ?? string.Empty) : string.Empty;
					var job = r.job;
					// 特质与无法从事
					var traits = r.prompt?.Traits?.Traits ?? new List<string>();
					var disables = r.prompt?.Traits?.WorkDisables ?? new List<string>();
					// 技能（压缩到 Top3 名称）
					var skills = r.prompt?.Skills?.Items ?? new List<RimAI.Core.Source.Modules.World.SkillItem>();
					var topSkills = skills.OrderByDescending(s => s.Level).Take(3).Select(s => s.Name).ToList();
					// 需求与状态（挑选 1-2 个最弱项）
					var needList = new List<(string key, float val)> {
						("Food", r.needs?.Food ?? 0f), ("Rest", r.needs?.Rest ?? 0f), ("Recreation", r.needs?.Recreation ?? 0f), ("Mood", r.needs?.Mood ?? 0f)
					};
					var weak = needList.OrderBy(x => x.val).Take(2).Select(x => x.key).ToList();
					// 健康（汇总死亡与平均占位）
					var healthState = r.health?.IsDead == true ? "已死亡" : "存活";

					// 意识形态 + 固定提示词 + 社交（单行内联，去除换行）
					string ideologyText = string.Empty;
					string fixedText = string.Empty;
					string socialText = string.Empty;
					try
					{
						var entityId = $"pawn:{r.id}";
						var persona = personaSvc?.Get(entityId);
						if (persona?.Ideology != null)
						{
							var segs = new List<string>();
							if (!string.IsNullOrWhiteSpace(persona.Ideology.Worldview)) segs.Add(persona.Ideology.Worldview.Trim());
							if (!string.IsNullOrWhiteSpace(persona.Ideology.Values)) segs.Add(persona.Ideology.Values.Trim());
							if (!string.IsNullOrWhiteSpace(persona.Ideology.CodeOfConduct)) segs.Add(persona.Ideology.CodeOfConduct.Trim());
							if (!string.IsNullOrWhiteSpace(persona.Ideology.TraitsText)) segs.Add(persona.Ideology.TraitsText.Trim());
							ideologyText = CleanInline(string.Join("/", segs));
						}
						if (persona?.FixedPrompts != null && !string.IsNullOrWhiteSpace(persona.FixedPrompts.Text))
						{
							fixedText = CleanInline(persona.FixedPrompts.Text);
						}
					}
					catch { }
					try
					{
						var social = await world.GetPawnSocialSnapshotAsync(r.id, 2, 0, ct).ConfigureAwait(false);
						if (social?.Relations != null && social.Relations.Count > 0)
						{
							socialText = CleanInline(string.Join("/", social.Relations.Take(2).Select(rel => $"{rel.RelationKind}-{rel.OtherName}({rel.Opinion:+#;-#;0})")));
						}
					}
					catch { }

					var args = new Dictionary<string, string>
					{
						{ "index", index.ToString() },
						{ "name", name },
						{ "gender", gender },
						{ "age", age },
						{ "job", job ?? string.Empty },
						{ "traits", string.Join("/", traits.Take(2)) },
						{ "disables", string.Join("/", disables.Take(3)) },
						{ "skills", string.Join("/", topSkills) },
						{ "needs", string.Join("/", weak) },
						{ "health", healthState },
						{ "belief", r.prompt?.Id?.Belief ?? string.Empty },
						{ "ideology", string.IsNullOrWhiteSpace(ideologyText) ? "-" : ideologyText },
						{ "fixed", string.IsNullOrWhiteSpace(fixedText) ? "-" : fixedText },
						{ "social", string.IsNullOrWhiteSpace(socialText) ? "-" : socialText }
					};
					string line = null;
					try { line = ctx?.F?.Invoke("stage.groupchat.participant.line", args, null); } catch { line = null; }
					if (string.IsNullOrWhiteSpace(line))
					{
						line = $"{index}) {name} / {gender}{(string.IsNullOrWhiteSpace(age)?"":("/"+age+"岁"))}{(string.IsNullOrWhiteSpace(job)?"":"，"+job)}；特质：{args["traits"]}；不可：{args["disables"]}；技能：{args["skills"]}；需求：{args["needs"]}；健康：{args["health"]}；意识形态：{args["ideology"]}；固定提示词：{args["fixed"]}；社交：{args["social"]}";
					}
					lines.Add(line);
				}
				// 追加：群聊 JSON 合约与白名单（严格输出数组对象，不含解释文本）
				var whitelist = string.Join(", ", participants.Select((id, i) => $"{i + 1}:{id}"));
				lines.Add($"仅输出 JSON 数组，每个元素形如 {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}；发言者必须在白名单内：[{whitelist}]；不得输出解释文本或额外内容。");
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() };
		}

		private static string CleanInline(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return string.Empty;
			var t = s.Replace("\r", " ").Replace("\n", " ");
			while (t.Contains("  ")) t = t.Replace("  ", " ");
			return t.Trim();
		}
	}
}


