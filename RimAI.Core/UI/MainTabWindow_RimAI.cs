using RimWorld;
using UnityEngine;
using Verse;
using System.Threading.Tasks;
using System.Collections.Generic;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Models;
using System.Linq;
using RimAI.Core.Settings;
using System;

namespace RimAI.Core.UI
{
    public class MainTabWindow_RimAI : MainTabWindow
    {
        private List<ChatMessage> _displayMessages = new List<ChatMessage>();
        private Vector2 _scrollPosition;
        private string _currentInput = "";
        private bool _isProcessing = false; // Re-add this field
        
        // Use the global ID provider instead of local constants
        public string PlayerId => CoreServices.PlayerStableId; // Corrected to PlayerStableId
        private const string AiId = "Governor";
        private string _conversationId;

        public override void PreOpen()
        {
            base.PreOpen();
            
            // 检查服务是否已初始化
            if (!CoreServices.AreServicesReady())
            {
                Log.Error("[MainTabWindow_RimAI] Core services are not initialized. Please check your mod settings.");
                Messages.Message("RimAI Core services are not initialized. Please check the mod settings.", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 额外的空值检查
            if (CoreServices.History == null)
            {
                Log.Error("[MainTabWindow_RimAI] History service is null even though services reported ready.");
                return;
            }
            
            // Initialize conversation when the window is opened
            if (string.IsNullOrEmpty(_conversationId))
            {
                try
                {
                    // 直接使用 CoreServices.PlayerStableId 并处理可能的异常
                    var playerId = CoreServices.PlayerStableId;
                    _conversationId = CoreServices.History.StartOrGetConversation(new List<string> { playerId, AiId });
                    // Potentially load existing messages from history here if desired
                }
                catch (Exception ex)
                {
                    Log.Error($"[MainTabWindow_RimAI] Failed to initialize conversation: {ex}");
                    _conversationId = "fallback_conversation";
                }
            }
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // --- 1. Conversation History View ---
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, CalculateHistoryHeight());
            Rect scrollRect = listing.GetRect(inRect.height - 80f); // Leave space for input
            
            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect, true);
            var historyListing = new Listing_Standard();
            historyListing.Begin(viewRect);

            foreach(var message in _displayMessages)
            {
                DrawMessage(historyListing, message);
            }

            historyListing.End();
            Widgets.EndScrollView();
            
            // --- 2. Input Area ---
            listing.Gap(4f);
            Rect inputRect = listing.GetRect(40f);
            Rect textFieldRect = new Rect(inputRect.x, inputRect.y, inputRect.width - 80f, inputRect.height);
            Rect buttonRect = new Rect(textFieldRect.xMax + 10f, inputRect.y, 70f, inputRect.height);

            _currentInput = GUI.TextField(textFieldRect, _currentInput);
            
            if (Widgets.ButtonText(buttonRect, "Send") && !_isProcessing)
            {
                HandleSend();
            }

            listing.End();
        }

        private void DrawMessage(Listing_Standard listing, ChatMessage message)
        {
            float width = listing.ColumnWidth;
            float height = Text.CalcHeight(message.Content, width);
            Rect rect = listing.GetRect(height);

            GUI.color = message.Role == "user" ? new Color(0.8f, 0.9f, 1f) : Color.white;
            Widgets.Label(rect, $"<b>{message.Name ?? message.Role}:</b> {message.Content}");
            GUI.color = Color.white;
            listing.Gap(4f);
        }

        private float CalculateHistoryHeight()
        {
            float height = 0f;
            foreach (var message in _displayMessages)
            {
                height += Text.CalcHeight(message.Content, 350f) + 10f; // Approximate width
            }
            return height;
        }

        private async void HandleSend()
        {
            if (string.IsNullOrWhiteSpace(_currentInput)) return;
            
            if (!CoreServices.AreServicesReady())
            {
                Messages.Message("RimAI services are not ready. Please try again later.", MessageTypeDefOf.RejectInput);
                return;
            }

            var userInput = _currentInput;
            _currentInput = "";

            var playerMessage = new ChatMessage { Role = "user", Content = userInput, Name = SettingsManager.Settings.Player.Nickname };
            _displayMessages.Add(playerMessage);
            CoreServices.History.AddEntry(_conversationId, new ConversationEntry { ParticipantId = PlayerId, Role = "user", Content = userInput, GameTicksTimestamp = CoreServices.SafeAccessService.GetTicksGameSafe() });
            ScrollToBottom();
            
            _isProcessing = true;

            var thinkingMessage = new ChatMessage { Role = "assistant", Content = "", Name = "Governor" };
            _displayMessages.Add(thinkingMessage);
            ScrollToBottom();
            
            try
            {
                var fullResponse = await CoreServices.Governor.HandleUserQueryStreamAsync(userInput, chunk =>
                {
                    // This action is called for each piece of the response.
                    thinkingMessage.Content += chunk;
                    ScrollToBottom(); // Keep scrolling to the bottom as new content arrives
                });

                // Now that we have the full response, add it to the history.
                CoreServices.History.AddEntry(_conversationId, new ConversationEntry { ParticipantId = AiId, Role = "assistant", Content = fullResponse, GameTicksTimestamp = CoreServices.SafeAccessService.GetTicksGameSafe() });
            }
            catch (System.Exception ex)
            {
                thinkingMessage.Content = $"Error: {ex.Message}";
                Log.Error($"[MainTabWindow_RimAI] Error handling send: {ex}");
            }
            finally
            {
                _isProcessing = false;
                ScrollToBottom(); // Final scroll to make sure
            }
        }
        
        private void ScrollToBottom()
        {
            // This ensures the scroll view jumps to the latest message
            _scrollPosition.y = CalculateHistoryHeight();
        }
    }
}
