using System;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persona.FixedPrompt
{
	internal sealed class FixedPromptService : IFixedPromptService
	{
		private readonly IPersistenceService _persistence;

		public FixedPromptService(IPersistenceService persistence) { _persistence = persistence; }

		public RimAI.Core.Source.Modules.Persona.FixedPromptSnapshot Get(string entityId)
		{
			var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
			if (snap.FixedPrompts.Items.TryGetValue(entityId, out var t))
			{
				return new RimAI.Core.Source.Modules.Persona.FixedPromptSnapshot { Text = t ?? string.Empty, UpdatedAtUtc = DateTime.UtcNow };
			}
			return new RimAI.Core.Source.Modules.Persona.FixedPromptSnapshot();
		}

		public void Set(string entityId, string text)
		{
			var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
			snap.FixedPrompts.Items[entityId] = text ?? string.Empty;
			_persistence.ReplaceLastSnapshotForDebug(snap);
		}
	}
}


