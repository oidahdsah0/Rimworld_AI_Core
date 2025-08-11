using System;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.World;
using RimWorld;
using UnityEngine;

namespace RimAI.Core.Modules.Stage.Bubbles
{
    /// <summary>
    /// 订阅群聊回合完成事件，在主线程以气泡显示发言文本（截断）。
    /// </summary>
    internal sealed class StageBubbleSink
    {
        private readonly IEventBus _events;
        private readonly IParticipantIdService _pid;
        private readonly Infrastructure.Configuration.IConfigurationService _config;

        public StageBubbleSink(IEventBus events, IParticipantIdService pid, Infrastructure.Configuration.IConfigurationService config)
        {
            _events = events;
            _pid = pid;
            _config = config;
            try { _events.Subscribe<OrchestrationProgressEvent>(OnProgress); } catch { }
        }

        private void OnProgress(OrchestrationProgressEvent e)
        {
            try
            {
                if (!string.Equals(e?.Source, "GroupChatAct", StringComparison.Ordinal)) return;
                if (!string.Equals(e?.Stage, "TurnCompleted", StringComparison.Ordinal)) return;

                var stageCfg = _config.Current?.Stage;
                // 可扩展：ShowBubbles 开关，当前默认开启（未提供开关字段时默认 true）
                int maxChars = 100;
                try
                {
                    if (!string.IsNullOrWhiteSpace(e.PayloadJson))
                    {
                        var obj = JObject.Parse(e.PayloadJson);
                        var speakerId = obj.Value<string>("speakerId");
                        var ok = obj.Value<bool?>("ok") ?? true;
                        if (!ok) return; // 失败回合不显示

                        // 获取最近一次历史写入的内容不可行，此处仅展示“回合完成”提示，后续可在事件中携带摘要文本再显示。
                        // 暂用简短提示（可改为传递文本片段到事件payload）
                        var text = obj.Value<string>("text");
                        if (string.IsNullOrWhiteSpace(text)) return;
                        text = Truncate(text, maxChars);

                        // 主线程显示（需 Mote/气泡接口，留空：根据实际游戏 API 实现）
                        CoreServices.Locator.Get<ISchedulerService>()
                            .ScheduleOnMainThreadAsync(() => TryShowBubble(speakerId, text));
                    }
                }
                catch { }
            }
            catch { }
        }

        private static string Truncate(string text, int maxChars)
        {
            text = text ?? string.Empty;
            if (maxChars <= 0 || text.Length <= maxChars) return text;
            return text.Substring(0, Math.Max(0, maxChars)).TrimEnd() + "…";
        }

        private static void TryShowBubble(string speakerId, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(speakerId) || string.IsNullOrWhiteSpace(text)) return;
                // 解析 Verse.Pawn
                Verse.Pawn pawn = null;
                try
                {
                    if (speakerId.StartsWith("pawn:"))
                    {
                        // 简化：遍历地图查找匹配 pawn（可优化为缓存/索引）
                        foreach (var map in Verse.Find.Maps)
                        {
                            var list = map?.mapPawns?.FreeColonistsSpawned;
                            if (list == null) continue;
                            foreach (var p in list)
                            {
                                var id = p?.GetUniqueLoadID() ?? p?.ThingID;
                                if (id != null && ($"pawn:{id}" == speakerId)) { pawn = p; break; }
                            }
                            if (pawn != null) break;
                        }
                    }
                }
                catch { }
                if (pawn == null) return;

                // 使用 RimWorld 的 MoteMaker 显示文本气泡
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, text, 3.5f);
            }
            catch { }
        }
    }
}


