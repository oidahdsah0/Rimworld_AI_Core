#nullable disable warnings
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimAI.Core.Infrastructure.Persistence
{
    /// <summary>
    /// 默认持久化服务，实现所有 Verse.Scribe 调用。仅此文件允许引用 Scribe。
    /// </summary>
    internal sealed class PersistenceService : IPersistenceService
    {
        private const string PersonasNode = "RimAI_Personas";
        private const string ConversationsNode = "RimAI_HistoryConversations";
        private const string InvertedIndexNode = "RimAI_HistoryInvertedIndex";
        private const string FixedPromptsNode = "RimAI_FixedPrompts"; // convKey => (pid => text)
        private const string BiographiesNode = "RimAI_Biographies";   // convKey => List<BiographyItem>
        private const string RecapNode = "RimAI_Recap";               // convKey => List<RecapItem>

        #region Serializable helpers
        private class SerConversationEntry : IExposable
        {
            public string SpeakerId = string.Empty!;
            public string Content = string.Empty!;
            public long Ticks;

            public SerConversationEntry() { }
            public SerConversationEntry(ConversationEntry src)
            {
                SpeakerId = src.SpeakerId;
                Content   = src.Content;
                Ticks     = src.Timestamp.Ticks;
            }
            public ConversationEntry ToModel() => new ConversationEntry(SpeakerId, Content, new System.DateTime(Ticks));
            public void ExposeData()
            {
                Scribe_Values.Look(ref SpeakerId, nameof(SpeakerId));
                Scribe_Values.Look(ref Content, nameof(Content));
                Scribe_Values.Look(ref Ticks, nameof(Ticks));
            }
        }

        private class SerConversationRecord : IExposable
        {
            public string ConvId = string.Empty;
            public List<SerConversationEntry> Entries = new();

            public SerConversationRecord() { }
            public SerConversationRecord(string id, Conversation conv)
            {
                ConvId  = id;
                Entries = conv.Entries.Select(e => new SerConversationEntry(e)).ToList();
            }
            public KeyValuePair<string, Conversation> ToModel()
            {
                var conv = new Conversation(Entries.Select(e => e.ToModel()).ToList());
                return new KeyValuePair<string, Conversation>(ConvId, conv);
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConvId, nameof(ConvId));
                Scribe_Collections.Look(ref Entries, nameof(Entries), LookMode.Deep);
            }
        }
        #endregion

        #region Serializable Persona helpers
        private class SerPersona : IExposable
        {
            public string Name = string.Empty!;
            public string SystemPrompt = string.Empty!;
            public Dictionary<string, string> Traits = new();

            public SerPersona() { }
            public SerPersona(Persona src)
            {
                Name = src.Name;
                SystemPrompt = src.SystemPrompt;
                Traits = src.Traits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            public Persona ToModel()
            {
                return new Persona(Name, SystemPrompt, Traits);
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref Name, nameof(Name));
                Scribe_Values.Look(ref SystemPrompt, nameof(SystemPrompt));
                Scribe_Collections.Look(ref Traits, nameof(Traits), LookMode.Value, LookMode.Value);
            }
        }
        #endregion

        public void PersistHistoryState(RimAI.Core.Services.IHistoryWriteService historyService)
        {
            if (historyService == null) return;

            var state = historyService.GetStateForPersistence();

            // --- Conversations ---
            var serRecords = state.PrimaryStore.Select(kvp => new SerConversationRecord(kvp.Key, kvp.Value)).ToList();
            Scribe_Collections.Look(ref serRecords, ConversationsNode, LookMode.Deep);

            // --- Inverted Index --- (convert HashSet -> List for serialization)
            var invIndex = state.InvertedIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
            Scribe_Collections.Look(ref invIndex, InvertedIndexNode, LookMode.Value, LookMode.Value);
        }

        public void LoadHistoryState(RimAI.Core.Services.IHistoryWriteService historyService)
        {
            if (historyService == null) return;

            var serRecords = new List<SerConversationRecord>();
            var invIndex  = new Dictionary<string, List<string>>();

            Scribe_Collections.Look(ref serRecords, ConversationsNode, LookMode.Deep);
            Scribe_Collections.Look(ref invIndex, InvertedIndexNode, LookMode.Value, LookMode.Value);

            // 处理加载时的空值情况
            if (serRecords == null) serRecords = new List<SerConversationRecord>();
            if (invIndex == null) invIndex = new Dictionary<string, List<string>>();

            // Rebuild primary store
            var primary = serRecords.Select(r => r.ToModel()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // Rebuild inverted index
            var inverted = invIndex.ToDictionary(kvp => kvp.Key, kvp => new HashSet<string>(kvp.Value));

            var newState = new HistoryState(primary, inverted);
            historyService.LoadStateFromPersistence(newState);
        }

        public void PersistPersonaState(IPersonaService personaService)
        {
            if (personaService == null) return;

            var state = personaService.GetStateForPersistence();
            var serList = state.Personas.Values.Select(p => new SerPersona(p)).ToList();
            Scribe_Collections.Look(ref serList, PersonasNode, LookMode.Deep);
        }

        public void LoadPersonaState(IPersonaService personaService)
        {
            if (personaService == null) return;

            var serList = new List<SerPersona>();
            Scribe_Collections.Look(ref serList, PersonasNode, LookMode.Deep);
            if (serList == null) serList = new List<SerPersona>();

            var dict = serList.ToDictionary(p => p.Name, p => p.ToModel(), StringComparer.OrdinalIgnoreCase);
            var newState = new PersonaState(dict);
            personaService.LoadStateFromPersistence(newState);
        }

        #region Fixed Prompts & Biographies
        private class SerFixedPromptEntry : IExposable
        {
            public string ConvKey = string.Empty;
            public Dictionary<string, string> Map = new();
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConvKey, nameof(ConvKey));
                Scribe_Collections.Look(ref Map, nameof(Map), LookMode.Value, LookMode.Value);
            }
        }

        private class SerBiographyItem : IExposable
        {
            public string Id;
            public string Text;
            public long Ticks;
            public SerBiographyItem() { }
            public SerBiographyItem(RimAI.Core.Modules.Persona.BiographyItem src)
            {
                Id = src.Id; Text = src.Text; Ticks = src.CreatedAt.Ticks;
            }
            public RimAI.Core.Modules.Persona.BiographyItem ToModel()
            {
                return new RimAI.Core.Modules.Persona.BiographyItem(Id, Text, new DateTime(Ticks));
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref Id, nameof(Id));
                Scribe_Values.Look(ref Text, nameof(Text));
                Scribe_Values.Look(ref Ticks, nameof(Ticks));
            }
        }

        private class SerBiographyRecord : IExposable
        {
            public string ConvKey = string.Empty;
            public List<SerBiographyItem> Items = new();
            public SerBiographyRecord() { }
            public SerBiographyRecord(string key, IReadOnlyList<RimAI.Core.Modules.Persona.BiographyItem> items)
            {
                ConvKey = key; Items = items.Select(i => new SerBiographyItem(i)).ToList();
            }
            public KeyValuePair<string, List<RimAI.Core.Modules.Persona.BiographyItem>> ToModel()
            {
                return new KeyValuePair<string, List<RimAI.Core.Modules.Persona.BiographyItem>>(ConvKey, Items.Select(i => i.ToModel()).ToList());
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConvKey, nameof(ConvKey));
                Scribe_Collections.Look(ref Items, nameof(Items), LookMode.Deep);
            }
        }

        private class SerRecapItem : IExposable
        {
            public string Id;
            public string Text;
            public long Ticks;
            public SerRecapItem() { }
            public SerRecapItem(RimAI.Core.Modules.History.RecapSnapshotItem src)
            {
                Id = src.Id; Text = src.Text; Ticks = src.CreatedAt.Ticks;
            }
            public RimAI.Core.Modules.History.RecapSnapshotItem ToModel() => new RimAI.Core.Modules.History.RecapSnapshotItem(Id, Text, new DateTime(Ticks));
            public void ExposeData()
            {
                Scribe_Values.Look(ref Id, nameof(Id));
                Scribe_Values.Look(ref Text, nameof(Text));
                Scribe_Values.Look(ref Ticks, nameof(Ticks));
            }
        }

        private class SerRecapRecord : IExposable
        {
            public string ConvKey = string.Empty;
            public List<SerRecapItem> Items = new();
            public SerRecapRecord() { }
            public SerRecapRecord(string key, IReadOnlyList<RimAI.Core.Modules.History.RecapSnapshotItem> items)
            {
                ConvKey = key; Items = items.Select(i => new SerRecapItem(i)).ToList();
            }
            public KeyValuePair<string, List<RimAI.Core.Modules.History.RecapSnapshotItem>> ToModel()
            {
                return new KeyValuePair<string, List<RimAI.Core.Modules.History.RecapSnapshotItem>>(ConvKey, Items.Select(i => i.ToModel()).ToList());
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConvKey, nameof(ConvKey));
                Scribe_Collections.Look(ref Items, nameof(Items), LookMode.Deep);
            }
        }

        public void PersistFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService)
        {
            if (fixedPromptService == null) return;
            var snap = fixedPromptService.ExportSnapshot();
            var list = snap.Select(kvp => new SerFixedPromptEntry { ConvKey = kvp.Key, Map = kvp.Value.ToDictionary(x => x.Key, x => x.Value) }).ToList();
            Scribe_Collections.Look(ref list, FixedPromptsNode, LookMode.Deep);
        }

        public void LoadFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService)
        {
            if (fixedPromptService == null) return;
            var list = new List<SerFixedPromptEntry>();
            Scribe_Collections.Look(ref list, FixedPromptsNode, LookMode.Deep);
            list ??= new List<SerFixedPromptEntry>();
            var snap = list.ToDictionary(e => e.ConvKey, e => (IReadOnlyDictionary<string, string>) (e.Map ?? new Dictionary<string, string>()));
            fixedPromptService.ImportSnapshot(snap);
        }

        public void PersistBiographies(RimAI.Core.Modules.Persona.IBiographyService biographyService)
        {
            if (biographyService == null) return;
            var snap = biographyService.ExportSnapshot();
            var list = snap.Select(kvp => new SerBiographyRecord(kvp.Key, kvp.Value)).ToList();
            Scribe_Collections.Look(ref list, BiographiesNode, LookMode.Deep);
        }

        public void LoadBiographies(RimAI.Core.Modules.Persona.IBiographyService biographyService)
        {
            if (biographyService == null) return;
            var list = new List<SerBiographyRecord>();
            Scribe_Collections.Look(ref list, BiographiesNode, LookMode.Deep);
            list ??= new List<SerBiographyRecord>();
            var snap = list.Select(r => r.ToModel()).ToDictionary(k => k.Key, v => (IReadOnlyList<RimAI.Core.Modules.Persona.BiographyItem>)v.Value);
            biographyService.ImportSnapshot(snap);
        }
        
        public void PersistRecap(RimAI.Core.Modules.History.IRecapService recapService)
        {
            if (recapService == null) return;
            var snap = recapService.ExportSnapshot();
            var list = snap.Select(kvp => new SerRecapRecord(kvp.Key, kvp.Value)).ToList();
            Scribe_Collections.Look(ref list, RecapNode, LookMode.Deep);
        }

        public void LoadRecap(RimAI.Core.Modules.History.IRecapService recapService)
        {
            if (recapService == null) return;
            var list = new List<SerRecapRecord>();
            Scribe_Collections.Look(ref list, RecapNode, LookMode.Deep);
            list ??= new List<SerRecapRecord>();
            var snap = list.Select(r => r.ToModel()).ToDictionary(k => k.Key, v => (IReadOnlyList<RimAI.Core.Modules.History.RecapSnapshotItem>)v.Value);
            recapService.ImportSnapshot(snap);
        }
        #endregion
    }
}
