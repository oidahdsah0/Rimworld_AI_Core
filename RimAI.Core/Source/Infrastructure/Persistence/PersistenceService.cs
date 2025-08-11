#nullable disable warnings
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Services;
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
        private const string ConversationsNode = "RimAI_HistoryV2_Conversations"; // conversationId -> record
        private const string ConvKeyIndexNode  = "RimAI_HistoryV2_ConvKeyIndex";  // convKey -> List<conversationId>
        private const string ParticipantIndexNode = "RimAI_HistoryV2_PartIndex"; // participantId -> List<conversationId>
        private const string FixedPromptsNode = "RimAI_FixedPromptsV2"; // pawnId -> text
        private const string BiographiesNode = "RimAI_BiographiesV2";   // pawnId => List<BiographyItem>
        private const string RecapNode = "RimAI_Recap";               // conversationId => List<RecapItem>
        private const string PersonaBindingsNode = "RimAI_PersonaBindingsV1"; // pawnId -> personaName#rev
        private const string PlayerIdNode = "RimAI_PlayerIdV1"; // player:<saveInstanceId>
        private const string PersonalBeliefsNode = "RimAI_PersonalBeliefsV1"; // pawnId -> PersonalBeliefs

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

        private class SerConversationRecordV2 : IExposable
        {
            public string ConvId = string.Empty; // conversationId (GUID)
            public List<string> ParticipantIds = new();
            public List<SerConversationEntry> Entries = new();

            public SerConversationRecordV2() { }
            public SerConversationRecordV2(ConversationRecord rec)
            {
                ConvId  = rec.ConversationId;
                ParticipantIds = rec.ParticipantIds?.ToList() ?? new List<string>();
                Entries = rec.Entries.Select(e => new SerConversationEntry(e)).ToList();
            }
            public ConversationRecord ToModel()
            {
                return new ConversationRecord(ConvId, ParticipantIds, Entries.Select(e => e.ToModel()).ToList());
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConvId, nameof(ConvId));
                Scribe_Collections.Look(ref ParticipantIds, nameof(ParticipantIds), LookMode.Value);
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

            var state = historyService.GetV2StateForPersistence();

            // --- Conversations (V2) ---
            var serRecords = state.Conversations.Values.Select(rec => new SerConversationRecordV2(rec)).ToList();
            Scribe_Collections.Look(ref serRecords, ConversationsNode, LookMode.Deep);

            // --- ConvKeyIndex ---
            var convKeyIndex = state.ConvKeyIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToList() ?? new List<string>());
            Scribe_Collections.Look(ref convKeyIndex, ConvKeyIndexNode, LookMode.Value, LookMode.Value);

            // --- ParticipantIndex ---
            var partIndex = state.ParticipantIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToList() ?? new List<string>());
            Scribe_Collections.Look(ref partIndex, ParticipantIndexNode, LookMode.Value, LookMode.Value);
        }

        public void LoadHistoryState(RimAI.Core.Services.IHistoryWriteService historyService)
        {
            if (historyService == null) return;

            var serRecords = new List<SerConversationRecordV2>();
            var convKeyIndex = new Dictionary<string, List<string>>();
            var partIndex = new Dictionary<string, List<string>>();

            Scribe_Collections.Look(ref serRecords, ConversationsNode, LookMode.Deep);
            Scribe_Collections.Look(ref convKeyIndex, ConvKeyIndexNode, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref partIndex, ParticipantIndexNode, LookMode.Value, LookMode.Value);

            serRecords ??= new List<SerConversationRecordV2>();
            convKeyIndex ??= new Dictionary<string, List<string>>();
            partIndex ??= new Dictionary<string, List<string>>();

            var conversations = new Dictionary<string, ConversationRecord>();
            foreach (var r in serRecords)
            {
                var rec = r.ToModel();
                conversations[rec.ConversationId] = rec;
            }
            var stateV2 = new HistoryV2State(conversations, convKeyIndex, partIndex);
            historyService.LoadV2StateFromPersistence(stateV2);
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

        #region Fixed Prompts & Biographies (现结构：convKey；后续将切换至 pawnId)
        private class SerFixedPromptV2 : IExposable
        {
            public string PawnId = string.Empty;
            public string Text = string.Empty;
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, nameof(PawnId));
                Scribe_Values.Look(ref Text, nameof(Text));
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
            public string PawnId = string.Empty;
            public List<SerBiographyItem> Items = new();
            public SerBiographyRecord() { }
            public SerBiographyRecord(string pawnId, IReadOnlyList<RimAI.Core.Modules.Persona.BiographyItem> items)
            {
                PawnId = pawnId; Items = items.Select(i => new SerBiographyItem(i)).ToList();
            }
            public KeyValuePair<string, List<RimAI.Core.Modules.Persona.BiographyItem>> ToModel()
            {
                return new KeyValuePair<string, List<RimAI.Core.Modules.Persona.BiographyItem>>(PawnId, Items.Select(i => i.ToModel()).ToList());
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, nameof(PawnId));
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
            public string ConversationId = string.Empty;
            public List<SerRecapItem> Items = new();
            public SerRecapRecord() { }
            public SerRecapRecord(string key, IReadOnlyList<RimAI.Core.Modules.History.RecapSnapshotItem> items)
            {
                ConversationId = key; Items = items.Select(i => new SerRecapItem(i)).ToList();
            }
            public KeyValuePair<string, List<RimAI.Core.Modules.History.RecapSnapshotItem>> ToModel()
            {
                return new KeyValuePair<string, List<RimAI.Core.Modules.History.RecapSnapshotItem>>(ConversationId, Items.Select(i => i.ToModel()).ToList());
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref ConversationId, nameof(ConversationId));
                Scribe_Collections.Look(ref Items, nameof(Items), LookMode.Deep);
            }
        }

        public void PersistFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService)
        {
            if (fixedPromptService == null) return;
            var map = fixedPromptService.ExportSnapshot(); // pawnId -> text
            var list = map.Select(kvp => new SerFixedPromptV2 { PawnId = kvp.Key, Text = kvp.Value ?? string.Empty }).ToList();
            Scribe_Collections.Look(ref list, FixedPromptsNode, LookMode.Deep);
        }

        public void LoadFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService)
        {
            if (fixedPromptService == null) return;
            var list = new List<SerFixedPromptV2>();
            Scribe_Collections.Look(ref list, FixedPromptsNode, LookMode.Deep);
            list ??= new List<SerFixedPromptV2>();
            var map = list.ToDictionary(e => e.PawnId ?? string.Empty, e => e.Text ?? string.Empty);
            fixedPromptService.ImportSnapshot(map);
        }

        public void PersistBiographies(RimAI.Core.Modules.Persona.IBiographyService biographyService)
        {
            if (biographyService == null) return;
            var snap = biographyService.ExportSnapshot(); // pawnId -> list
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

        // --- Persona Bindings ---
        private class SerPersonaBinding : IExposable
        {
            public string PawnId = string.Empty;
            public string PersonaName = string.Empty;
            public int Revision = 0;
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, nameof(PawnId));
                Scribe_Values.Look(ref PersonaName, nameof(PersonaName));
                Scribe_Values.Look(ref Revision, nameof(Revision));
            }
        }

        public void PersistPersonaBindings(RimAI.Core.Modules.Persona.IPersonaBindingService bindingService)
        {
            if (bindingService == null) return;
            var list = bindingService.GetAllBindings()
                .Select(b => new SerPersonaBinding { PawnId = b.PawnId, PersonaName = b.PersonaName, Revision = b.Revision })
                .ToList();
            Scribe_Collections.Look(ref list, PersonaBindingsNode, LookMode.Deep);
        }

        public void LoadPersonaBindings(RimAI.Core.Modules.Persona.IPersonaBindingService bindingService)
        {
            if (bindingService == null) return;
            var list = new List<SerPersonaBinding>();
            Scribe_Collections.Look(ref list, PersonaBindingsNode, LookMode.Deep);
            list ??= new List<SerPersonaBinding>();
            foreach (var b in list)
            {
                if (!string.IsNullOrWhiteSpace(b?.PawnId) && !string.IsNullOrWhiteSpace(b?.PersonaName))
                {
                    bindingService.Bind(b.PawnId, b.PersonaName, b.Revision);
                }
            }
        }

        public void PersistPlayerId(RimAI.Core.Modules.World.IParticipantIdService participantIdService)
        {
            if (participantIdService == null) return;
            var pid = participantIdService.ExportPlayerId();
            Scribe_Values.Look(ref pid, PlayerIdNode);
        }

        public void LoadPlayerId(RimAI.Core.Modules.World.IParticipantIdService participantIdService)
        {
            if (participantIdService == null) return;
            string pid = null;
            Scribe_Values.Look(ref pid, PlayerIdNode);
            if (!string.IsNullOrWhiteSpace(pid))
            {
                participantIdService.ImportPlayerId(pid);
            }
        }
        #endregion

        // --- Personal Beliefs ---
        private class SerPersonalBeliefs : IExposable
        {
            public string PawnId = string.Empty;
            public string Worldview = string.Empty;
            public string Values = string.Empty;
            public string CodeOfConduct = string.Empty;
            public string TraitsText = string.Empty;
            public SerPersonalBeliefs() { }
            public SerPersonalBeliefs(string pawnId, RimAI.Core.Modules.Persona.PersonalBeliefs src)
            {
                PawnId = pawnId ?? string.Empty;
                Worldview = src?.Worldview ?? string.Empty;
                Values = src?.Values ?? string.Empty;
                CodeOfConduct = src?.CodeOfConduct ?? string.Empty;
                TraitsText = src?.TraitsText ?? string.Empty;
            }
            public KeyValuePair<string, RimAI.Core.Modules.Persona.PersonalBeliefs> ToModel()
            {
                return new KeyValuePair<string, RimAI.Core.Modules.Persona.PersonalBeliefs>(
                    PawnId,
                    new RimAI.Core.Modules.Persona.PersonalBeliefs(Worldview, Values, CodeOfConduct, TraitsText));
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, nameof(PawnId));
                Scribe_Values.Look(ref Worldview, nameof(Worldview));
                Scribe_Values.Look(ref Values, nameof(Values));
                Scribe_Values.Look(ref CodeOfConduct, nameof(CodeOfConduct));
                Scribe_Values.Look(ref TraitsText, nameof(TraitsText));
            }
        }

        public void PersistPersonalBeliefs(RimAI.Core.Modules.Persona.IPersonalBeliefsAndIdeologyService beliefsService)
        {
            if (beliefsService == null) return;
            var map = beliefsService.ExportSnapshot();
            var list = new List<SerPersonalBeliefs>();
            foreach (var kv in map)
            {
                list.Add(new SerPersonalBeliefs(kv.Key, kv.Value));
            }
            Scribe_Collections.Look(ref list, PersonalBeliefsNode, LookMode.Deep);
        }

        public void LoadPersonalBeliefs(RimAI.Core.Modules.Persona.IPersonalBeliefsAndIdeologyService beliefsService)
        {
            if (beliefsService == null) return;
            var list = new List<SerPersonalBeliefs>();
            Scribe_Collections.Look(ref list, PersonalBeliefsNode, LookMode.Deep);
            list ??= new List<SerPersonalBeliefs>();
            var map = new Dictionary<string, RimAI.Core.Modules.Persona.PersonalBeliefs>(System.StringComparer.Ordinal);
            foreach (var r in list)
            {
                var kv = r.ToModel();
                map[kv.Key] = kv.Value;
            }
            beliefsService.ImportSnapshot(map);
        }
    }
}
