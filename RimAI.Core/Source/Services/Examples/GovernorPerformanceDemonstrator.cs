using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using Verse;

namespace RimAI.Core.Services.Examples
{
    /// <summary>
    /// Demonstrates the new service-oriented architecture.
    /// </summary>
    public static class GovernorPerformanceDemonstrator
    {
        public static async Task RunDemonstration()
        {
            Log.Message("--- Governor Demonstration ---");

            var governor = CoreServices.Governor;
            if (governor == null)
            {
                Log.Error("Governor service not found. Cannot run demonstration.");
                return;
            }

            // --- Test 1: Standard Advice ---
            await MeasureAndLogAsync(
                "Standard Advice",
                async () => await governor.ProvideAdviceAsync()
            );

            // --- Test 2: User Query ---
            await MeasureAndLogAsync(
                "User Query ('food status')",
                async () => await governor.HandleUserQueryAsync("What is the status of our food supplies?")
            );

            // --- Test 3: History and Context ---
            Log.Message("[DEMO] Simulating a short conversation to test history...");
            var history = CoreServices.History;
            var playerId = CoreServices.PlayerStableId;
            var aiId = "Governor";
            var conversationId = history.StartOrGetConversation(new System.Collections.Generic.List<string> { playerId, aiId });
            history.AddEntry(conversationId, new Architecture.Models.ConversationEntry { ParticipantId = playerId, Role = "user", Content = "Hello Governor, how are you?" });
            history.AddEntry(conversationId, new Architecture.Models.ConversationEntry { ParticipantId = aiId, Role = "assistant", Content = "I am fine, Commander. What can I do for you?" });
            
            await MeasureAndLogAsync(
                "Follow-up Query (with history)",
                async () => await governor.HandleUserQueryAsync("Based on our last talk, what should we do next?")
            );

            Log.Message("--- Demonstration Complete ---");
        }

        private static async Task MeasureAndLogAsync(string testName, Func<Task<string>> action)
        {
            var stopwatch = new Stopwatch();
            string result;

            Log.Message($"[DEMO] Running: {testName}...");
            stopwatch.Start();
            try
            {
                result = await action();
            }
            catch(Exception ex)
            {
                result = $"Error: {ex.Message}";
            }
            stopwatch.Stop();

            Log.Message($"[DEMO] Finished: {testName} in {stopwatch.ElapsedMilliseconds}ms.");
            // We don't log the full result anymore as it can be a very long prompt payload string.
            // Log.Message($"[DEMO] Result: {result}");
        }
    }
}
