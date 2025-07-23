using System;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Settings;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// A factory responsible for creating the appropriate IDispatcherService instance
    /// based on the current application settings. This is the heart of the Strategy Pattern,
    /// allowing for dynamic switching of the AI's tool-selection behavior.
    /// </summary>
    public static class DispatcherFactory
    {
        private static IDispatcherService _llmToolInstance;
        private static IDispatcherService _llmJsonInstance;
        private static IDispatcherService _embeddingInstance;

        /// <summary>
        /// Creates and returns an instance of the currently selected dispatcher service.
        /// It uses lazy initialization to create instances only when they are first needed.
        /// </summary>
        /// <returns>An object that implements IDispatcherService.</returns>
        public static IDispatcherService Create()
        {
            // Read the user's choice from the central settings.
            var mode = SettingsManager.Settings.AIPilot.DispatcherMode;

            switch (mode)
            {
                case DispatchMode.LlmTool:
                    return _llmToolInstance ??= new LlmToolDispatcherService();

                case DispatchMode.LlmJson:
                    return _llmJsonInstance ??= new LlmJsonDispatcherService();

                case DispatchMode.LocalEmbedding:
                    return _embeddingInstance ??= new EmbeddingDispatcherService();

                default:
                    // Fallback to the most reliable option.
                    Log.Warning($"[DispatcherFactory] Unknown DispatchMode '{mode}'. Falling back to LlmTool.");
                    return _llmToolInstance ??= new LlmToolDispatcherService();
            }
        }
    }
} 