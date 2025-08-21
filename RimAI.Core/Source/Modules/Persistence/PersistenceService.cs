using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;

namespace RimAI.Core.Source.Modules.Persistence
{
    internal sealed class PersistenceService : IPersistenceService
	{
        private readonly string _root;
        private PersistenceStats _lastStats;
        private readonly IConfigurationService _configurationService;
        private int _maxTextLength = 4000;
        private PersistenceSnapshot _lastSnapshot;
        private PersistenceSnapshot _importBuffer;

        public PersistenceService(IConfigurationService configurationService)
		{
            _configurationService = configurationService;
            LoadConfig(configurationService as ConfigurationService);
            if (_configurationService != null)
            {
                _configurationService.OnConfigurationChanged += _ => LoadConfig(_configurationService as ConfigurationService);
            }
			// 使用 RimWorld 存档目录作为配置根，避免写入 Mod 目录
			var baseDir = GenFilePaths.SaveDataFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			_root = Path.Combine(baseDir, "RimAI");
			Directory.CreateDirectory(_root);
		}

        private void LoadConfig(ConfigurationService cfg)
        {
            try { _maxTextLength = Math.Max(0, cfg?.GetInternal()?.Persistence?.MaxTextLength ?? 4000); } catch { _maxTextLength = 4000; }
        }

        public string GetConfigRootDirectory() => _root;

		public string EnsureDirectoryUnderConfig(string relativeDir)
		{
			var abs = Path.Combine(_root, relativeDir ?? string.Empty);
			Directory.CreateDirectory(abs);
			return abs;
		}

        public void SaveAll(PersistenceSnapshot snapshot)
        {
			if (_importBuffer != null)
			{
				// 优先写入导入的快照，并在成功后清空缓冲
				snapshot = _importBuffer;
				_importBuffer = null;
			}
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            var stats = new PersistenceStats { Operation = "save" };
            // v1 实现：仅写入空节点元数据与 schemaVersion；后续迭代填充
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                // ConversationsV2
                Scribe.EnterNode("RimAI_ConversationsV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
				var convs = snapshot?.History?.Conversations ?? new System.Collections.Generic.Dictionary<string, Snapshots.ConversationRecord>();
                Scribe_Poco.LookJsonDict(ref convs, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ConversationsV2", Ok = true, Entries = convs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ConversationsV2", Ok = false, Error = ex.Message });
            }
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ConvKeyIndexV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx = snapshot?.History?.ConvKeyIndex ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                Scribe_Dict.Look(ref idx, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ConvKeyIndexV2", Ok = true, Entries = idx?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ConvKeyIndexV2", Ok = false, Error = ex.Message });
            }
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ParticipantIndexV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx2 = snapshot?.History?.ParticipantIndex ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                Scribe_Dict.Look(ref idx2, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ParticipantIndexV2", Ok = true, Entries = idx2?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ParticipantIndexV2", Ok = false, Error = ex.Message });
            }
			// Recap（按配置最大长度裁剪）
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_RecapV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
				var recaps = snapshot?.Recap?.Recaps ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Snapshots.RecapSnapshotItem>>();
				if (recaps != null)
				{
					foreach (var list in recaps.Values)
					{
						if (list == null) continue;
						for (int i = 0; i < list.Count; i++)
						{
							var t = list[i]?.Text ?? string.Empty;
							if (t.Length > _maxTextLength)
							{
								list[i].Text = t.Substring(0, _maxTextLength);
							}
						}
					}
				}
                Scribe_Poco.LookJsonDict(ref recaps, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_RecapV1", Ok = true, Entries = recaps?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_RecapV1", Ok = false, Error = ex.Message });
            }
            // FixedPrompts
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_FixedPromptsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var fixedPrompts = snapshot?.FixedPrompts?.Items ?? new System.Collections.Generic.Dictionary<string, string>();
                Scribe_Dict.Look(ref fixedPrompts, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_FixedPromptsV1", Ok = true, Entries = fixedPrompts?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_FixedPromptsV1", Ok = false, Error = ex.Message });
            }
            // PersonaJob
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonaJobV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var jobs = snapshot?.PersonaJob?.Items ?? new System.Collections.Generic.Dictionary<string, Snapshots.PersonaJob>();
                Scribe_Poco.LookJsonDict(ref jobs, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaJobV1", Ok = true, Entries = jobs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaJobV1", Ok = false, Error = ex.Message });
            }
			// Biographies（按配置最大长度裁剪）
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_BiographiesV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
				var bios = snapshot?.Biographies?.Items ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Snapshots.BiographyItem>>();
				if (bios != null)
				{
					foreach (var list in bios.Values)
					{
						if (list == null) continue;
						for (int i = 0; i < list.Count; i++)
						{
							var t = list[i]?.Text ?? string.Empty;
							if (t.Length > _maxTextLength)
							{
								list[i].Text = t.Substring(0, _maxTextLength);
							}
						}
					}
				}
                Scribe_Poco.LookJsonDict(ref bios, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_BiographiesV1", Ok = true, Entries = bios?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_BiographiesV1", Ok = false, Error = ex.Message });
            }
            // Persona Bindings
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonaBindingsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var bindings = snapshot?.PersonaBindings?.Items ?? new System.Collections.Generic.Dictionary<string, string>();
                Scribe_Dict.Look(ref bindings, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaBindingsV1", Ok = true, Entries = bindings?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaBindingsV1", Ok = false, Error = ex.Message });
            }
            // PersonalBeliefs
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonalBeliefsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var beliefs = snapshot?.PersonalBeliefs?.Items ?? new System.Collections.Generic.Dictionary<string, Snapshots.PersonalBeliefs>();
                Scribe_Poco.LookJsonDict(ref beliefs, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonalBeliefsV1", Ok = true, Entries = beliefs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonalBeliefsV1", Ok = false, Error = ex.Message });
            }
            // StageRecap
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_StageRecapV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var stage = snapshot?.StageRecap?.Items ?? new System.Collections.Generic.List<Snapshots.ActRecapEntry>();
                Scribe_Poco.LookJsonList(ref stage, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_StageRecapV1", Ok = true, Entries = stage?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_StageRecapV1", Ok = false, Error = ex.Message });
            }
            // P13: Servers
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ServersV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var servers = snapshot?.Servers ?? new Snapshots.ServerState();
                RimAI.Core.Source.Modules.Persistence.ScribeAdapters.Scribe_Poco.LookJson(ref servers, "state");
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ServersV1", Ok = true, Entries = servers?.Items?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ServersV1", Ok = false, Error = ex.Message });
            }
            swAll.Stop();
            stats.Nodes = stats.Details.Count;
            stats.ElapsedMs = swAll.ElapsedMilliseconds;
            _lastStats = stats;
            _lastSnapshot = snapshot ?? new PersistenceSnapshot();
            Log.Message($"[RimAI.Core][P6.Persistence] op=save, nodes={stats.Nodes}, elapsed={stats.ElapsedMs}ms");
        }

        public PersistenceSnapshot LoadAll()
        {
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            var stats = new PersistenceStats { Operation = "load" };
            var result = new PersistenceSnapshot();
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ConversationsV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var convs = result.History.Conversations;
                Scribe_Poco.LookJsonDict(ref convs, "items");
                convs ??= new System.Collections.Generic.Dictionary<string, Snapshots.ConversationRecord>();
                result.History.Conversations = convs;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ConversationsV2", Ok = true, Entries = convs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ConversationsV2", Ok = false, Error = ex.Message });
            }
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ConvKeyIndexV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx = result.History.ConvKeyIndex;
                Scribe_Dict.Look(ref idx, "items");
                idx ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                result.History.ConvKeyIndex = idx;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ConvKeyIndexV2", Ok = true, Entries = idx?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ConvKeyIndexV2", Ok = false, Error = ex.Message });
            }
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ParticipantIndexV2");
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx2 = result.History.ParticipantIndex;
                Scribe_Dict.Look(ref idx2, "items");
                idx2 ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                result.History.ParticipantIndex = idx2;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ParticipantIndexV2", Ok = true, Entries = idx2?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ParticipantIndexV2", Ok = false, Error = ex.Message });
            }
            // Recap
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_RecapV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var recaps = result.Recap.Recaps;
                Scribe_Poco.LookJsonDict(ref recaps, "items");
                recaps ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Snapshots.RecapSnapshotItem>>();
                result.Recap.Recaps = recaps;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_RecapV1", Ok = true, Entries = recaps?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_RecapV1", Ok = false, Error = ex.Message });
            }
            // FixedPrompts
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_FixedPromptsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var fixedPrompts = result.FixedPrompts.Items;
                Scribe_Dict.Look(ref fixedPrompts, "items");
                fixedPrompts ??= new System.Collections.Generic.Dictionary<string, string>();
                result.FixedPrompts.Items = fixedPrompts;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_FixedPromptsV1", Ok = true, Entries = fixedPrompts?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_FixedPromptsV1", Ok = false, Error = ex.Message });
            }
            // PersonaJob
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonaJobV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var jobs = result.PersonaJob.Items;
                Scribe_Poco.LookJsonDict(ref jobs, "items");
                jobs ??= new System.Collections.Generic.Dictionary<string, Snapshots.PersonaJob>();
                result.PersonaJob.Items = jobs;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaJobV1", Ok = true, Entries = jobs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaJobV1", Ok = false, Error = ex.Message });
            }
            // Biographies
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_BiographiesV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var bios = result.Biographies.Items;
                Scribe_Poco.LookJsonDict(ref bios, "items");
                bios ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Snapshots.BiographyItem>>();
                result.Biographies.Items = bios;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_BiographiesV1", Ok = true, Entries = bios?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_BiographiesV1", Ok = false, Error = ex.Message });
            }
            // Persona Bindings
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonaBindingsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var bindings = result.PersonaBindings.Items;
                Scribe_Dict.Look(ref bindings, "items");
                bindings ??= new System.Collections.Generic.Dictionary<string, string>();
                result.PersonaBindings.Items = bindings;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaBindingsV1", Ok = true, Entries = bindings?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonaBindingsV1", Ok = false, Error = ex.Message });
            }
            // PersonalBeliefs
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_PersonalBeliefsV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var beliefs = result.PersonalBeliefs.Items;
                Scribe_Poco.LookJsonDict(ref beliefs, "items");
                beliefs ??= new System.Collections.Generic.Dictionary<string, Snapshots.PersonalBeliefs>();
                result.PersonalBeliefs.Items = beliefs;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonalBeliefsV1", Ok = true, Entries = beliefs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_PersonalBeliefsV1", Ok = false, Error = ex.Message });
            }
            // StageRecap
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_StageRecapV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var stage = result.StageRecap.Items;
                Scribe_Poco.LookJsonList(ref stage, "items");
                stage ??= new System.Collections.Generic.List<Snapshots.ActRecapEntry>();
                result.StageRecap.Items = stage;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_StageRecapV1", Ok = true, Entries = stage?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_StageRecapV1", Ok = false, Error = ex.Message });
            }
            // P13: Servers
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode("RimAI_ServersV1");
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var servers = result.Servers;
                RimAI.Core.Source.Modules.Persistence.ScribeAdapters.Scribe_Poco.LookJson(ref servers, "state");
                servers ??= new Snapshots.ServerState();
                result.Servers = servers;
                Scribe.ExitNode();
                nodeSw.Stop();
                stats.Details.Add(new NodeStat { Node = "RimAI_ServersV1", Ok = true, Entries = servers?.Items?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                stats.Details.Add(new NodeStat { Node = "RimAI_ServersV1", Ok = false, Error = ex.Message });
            }
            swAll.Stop();
            stats.Nodes = stats.Details.Count;
            stats.ElapsedMs = swAll.ElapsedMilliseconds;
            _lastStats = stats;
            _lastSnapshot = result;
            // 读档后索引自检/重建
            try
            {
                var cfgImpl = _configurationService as ConfigurationService;
                var onLoadRebuild = cfgImpl?.GetInternal()?.Persistence?.OnLoadRebuildIndexes ?? true;
                if (onLoadRebuild)
                {
                    var fixedCounts = RebuildHistoryIndexesIfNeeded(result);
                    if (fixedCounts.convKeyFixed > 0 || fixedCounts.participantFixed > 0)
                    {
                        Log.Message($"[RimAI.Core][P6.Persistence] rebuild=HistoryIndexes, convKeyFixed={fixedCounts.convKeyFixed}, participantFixed={fixedCounts.participantFixed}");
                    }
                }
            }
            catch { }
            Log.Message($"[RimAI.Core][P6.Persistence] op=load, nodes={stats.Nodes}, elapsed={stats.ElapsedMs}ms");
            return result;
        }

        public PersistenceStats GetLastStats() => _lastStats;

        public string ExportAllToJson() => JsonConvert.SerializeObject(_lastSnapshot ?? new PersistenceSnapshot(), Formatting.Indented);

        public void ImportAllFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json is empty");
            // 解析并缓存导入的快照，等待下一次 SaveAll（在 ExposeData 内）写入
            _importBuffer = JsonConvert.DeserializeObject<PersistenceSnapshot>(json) ?? new PersistenceSnapshot();
            _lastSnapshot = _importBuffer;
        }

        public PersistenceSnapshot GetLastSnapshotForDebug() => _lastSnapshot ?? new PersistenceSnapshot();

        public void ReplaceLastSnapshotForDebug(PersistenceSnapshot snapshot)
        {
            _lastSnapshot = snapshot ?? new PersistenceSnapshot();
        }

        private (int convKeyFixed, int participantFixed) RebuildHistoryIndexesIfNeeded(PersistenceSnapshot snap)
        {
            int ck = 0, pk = 0;
            if (snap?.History == null) return (0, 0);
            var convs = snap.History.Conversations ?? new System.Collections.Generic.Dictionary<string, Snapshots.ConversationRecord>();
            if (convs.Count == 0) return (0, 0);
            var convKeyIdx = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            var partIdx = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var kv in convs)
            {
                var convId = kv.Key;
                var cr = kv.Value;
                if (cr?.ParticipantIds == null || cr.ParticipantIds.Count == 0) continue;
                var convKey = string.Join("|", cr.ParticipantIds.OrderBy(x => x));
                if (!convKeyIdx.TryGetValue(convKey, out var list1)) { list1 = new System.Collections.Generic.List<string>(); convKeyIdx[convKey] = list1; }
                if (!list1.Contains(convId)) list1.Add(convId);
                foreach (var pid in cr.ParticipantIds)
                {
                    if (!partIdx.TryGetValue(pid, out var list2)) { list2 = new System.Collections.Generic.List<string>(); partIdx[pid] = list2; }
                    if (!list2.Contains(convId)) list2.Add(convId);
                }
            }
            // Compare & assign
            if (snap.History.ConvKeyIndex == null || snap.History.ConvKeyIndex.Count != convKeyIdx.Count) ck = Math.Abs((snap.History.ConvKeyIndex?.Count ?? 0) - convKeyIdx.Count);
            if (snap.History.ParticipantIndex == null || snap.History.ParticipantIndex.Count != partIdx.Count) pk = Math.Abs((snap.History.ParticipantIndex?.Count ?? 0) - partIdx.Count);
            snap.History.ConvKeyIndex = convKeyIdx;
            snap.History.ParticipantIndex = partIdx;
            return (ck, pk);
        }

		public async Task WriteTextUnderConfigAsync(string relativePath, string content, CancellationToken ct = default)
		{
			var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
			var abs = Path.Combine(_root, normalized);
			var dir = Path.GetDirectoryName(abs);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
			using (var fs = new FileStream(abs, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
			{
				await fs.WriteAsync(bytes, 0, bytes.Length, ct);
				await fs.FlushAsync(ct);
			}
		}

		public async Task<string> ReadTextUnderConfigOrNullAsync(string relativePath, CancellationToken ct = default)
		{
			var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
			var abs = Path.Combine(_root, normalized);
			if (!File.Exists(abs)) return null;
			using (var fs = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous))
			using (var sr = new StreamReader(fs, Encoding.UTF8))
			{
				return await sr.ReadToEndAsync();
			}
		}

		public async Task<string> ReadTextUnderModRootOrNullAsync(string relativePath, CancellationToken ct = default)
		{
			try
			{
				var baseDir = RimAI.Core.Source.Boot.RimAICoreMod.ModRootDir ?? string.Empty;
				if (string.IsNullOrWhiteSpace(baseDir)) return null;
				var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
				var abs = Path.Combine(baseDir, normalized);
				if (!File.Exists(abs)) return null;
				using (var fs = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous))
				using (var sr = new StreamReader(fs, Encoding.UTF8))
				{
					return await sr.ReadToEndAsync();
				}
			}
			catch { return null; }
		}
	}
}



