using System;
using System.Collections.Concurrent;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimAI.Core.Modules.World
{
    /// <summary>
    /// 参与者稳定 ID 与显示名解析服务的默认实现（M1 占位）。
    /// - ID 规则：pawn:&lt;loadId&gt; / thing:&lt;loadId&gt; / faction:&lt;loadId&gt; / player:&lt;saveInstanceId&gt; / persona:&lt;name&gt;#&lt;rev&gt; / agent:&lt;guid&gt;
    /// - 显示名：维护最后可见名缓存；若不可得则回退到 ID。
    /// </summary>
    internal sealed class ParticipantIdService : IParticipantIdService
    {
        private readonly ConcurrentDictionary<string, string> _displayNameCache = new();
        private string _playerId; // 持久化的玩家 ID（格式：player:<saveInstanceId>）

        public string FromVerseObject(object verseObj)
        {
            if (verseObj is Pawn pawn)
            {
                var id = $"pawn:{pawn.GetUniqueLoadID() ?? pawn.ThingID ?? pawn.GetHashCode().ToString()}";
                var label = pawn?.Name?.ToStringShort ?? pawn?.LabelShortCap ?? pawn?.LabelCap ?? pawn?.def?.label ?? "Pawn";
                _displayNameCache[id] = label;
                return id;
            }
            if (verseObj is Thing thing)
            {
                var id = $"thing:{thing.GetUniqueLoadID() ?? thing.ThingID ?? thing.GetHashCode().ToString()}";
                var label = thing?.LabelCap ?? thing?.def?.label ?? "Thing";
                _displayNameCache[id] = label;
                return id;
            }
            if (verseObj is Faction faction)
            {
                var uid = faction?.loadID.ToString() ?? faction?.GetHashCode().ToString() ?? "0";
                var id = $"faction:{uid}";
                var label = faction?.Name ?? "Faction";
                _displayNameCache[id] = label;
                return id;
            }
            if (verseObj is Settlement settlement)
            {
                var uid = settlement != null ? settlement.Tile.ToString() : (verseObj?.GetHashCode().ToString() ?? "0");
                var id = $"settlement:{uid}";
                var label = settlement?.Label ?? "Settlement";
                _displayNameCache[id] = label;
                return id;
            }

            // 回退：未知对象类型
            var fallback = $"agent:{Guid.NewGuid():N}";
            _displayNameCache.TryAdd(fallback, verseObj?.ToString() ?? "Unknown");
            return fallback;
        }

        public string GetPlayerId()
        {
            if (!string.IsNullOrWhiteSpace(_playerId)) return _playerId;
            // 迟生成：首次访问创建稳定 ID；由持久化层在读档时回填。
            _playerId = $"player:{Guid.NewGuid():N}";
            _displayNameCache.TryAdd(_playerId, "玩家");
            return _playerId;
        }

        public string ForPersona(string name, int rev)
        {
            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name)) name = "Default";
            if (rev < 0) rev = 0;
            var id = $"persona:{name}#{rev}";
            _displayNameCache.TryAdd(id, name);
            return id;
        }

        public string GetDisplayName(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            if (_displayNameCache.TryGetValue(id, out var label) && !string.IsNullOrWhiteSpace(label))
                return label;
            // 简单解析：尝试取冒号后部分或 persona 的 name
            try
            {
                var idx = id.IndexOf(':');
                var tail = idx >= 0 && idx + 1 < id.Length ? id[(idx + 1)..] : id;
                if (id.StartsWith("persona:") && tail.Contains('#'))
                {
                    var name = tail.Split('#')[0];
                    return string.IsNullOrWhiteSpace(name) ? id : name;
                }
                return tail;
            }
            catch { return id; }
        }

        public string ExportPlayerId()
        {
            return _playerId ?? string.Empty;
        }

        public void ImportPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            if (!playerId.StartsWith("player:", StringComparison.Ordinal))
            {
                playerId = $"player:{playerId}";
            }
            _playerId = playerId;
            _displayNameCache.TryAdd(_playerId, "玩家");
        }
    }
}


