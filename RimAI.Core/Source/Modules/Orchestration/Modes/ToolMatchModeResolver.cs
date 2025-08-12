using System;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Orchestration.Modes
{
    internal sealed class ToolMatchModeResolver
    {
        private readonly Dictionary<string, IToolMatchMode> _modes;

        public ToolMatchModeResolver(
            ClassicMode classic,
            FastTop1Mode fastTop1,
            NarrowTopKMode narrowTopK,
            LightningFastMode lightning)
        {
            _modes = new Dictionary<string, IToolMatchMode>(StringComparer.OrdinalIgnoreCase)
            {
                [classic.Name] = classic,
                [fastTop1.Name] = fastTop1,
                [narrowTopK.Name] = narrowTopK,
                [lightning.Name] = lightning
            };
        }

        public IToolMatchMode Get(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) throw new ArgumentException("mode is required", nameof(mode));
            if (_modes.TryGetValue(mode, out var impl)) return impl;
            throw new ArgumentException($"Unknown tool match mode: {mode}", nameof(mode));
        }
    }
}


