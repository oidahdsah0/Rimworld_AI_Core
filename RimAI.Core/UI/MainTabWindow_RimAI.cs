using RimWorld;
using UnityEngine;
using Verse;
using System.Threading.Tasks;
using System.Collections.Generic;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Models;
using System.Linq;
using RimAI.Core.Settings;

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
            // Initialize conversation when the window is opened
            if (string.IsNullOrEmpty(_conversationId))
            {
                // Use the new PlayerId property
                _conversationId = CoreServices.History.StartOrGetConversation(new List<string> { PlayerId, AiId });
                // Potentially load existing messages from history here if desired
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

            var userInput = _currentInput;
            _currentInput = ""; // Clear input immediately
            _isProcessing = true;

            // Add player message to UI and history
            // Use the Nickname from settings for display
            var playerMessage = new ChatMessage { Role = "user", Content = userInput, Name = SettingsManager.Settings.Player.Nickname };
            _displayMessages.Add(playerMessage);
            ScrollToBottom();

            CoreServices.History.AddEntry(_conversationId, new ConversationEntry { ParticipantId = PlayerId, Role = "user", Content = userInput, GameTicksTimestamp = CoreServices.SafeAccessService.GetTicksGameSafe() });

            // Show a thinking indicator
            var thinkingMessage = new ChatMessage { Role = "assistant", Content = "...", Name = "Governor" };
            _displayMessages.Add(thinkingMessage);
            ScrollToBottom();
            
            try
            {
                // Get AI response
                var aiResponse = await CoreServices.Governor.HandleUserQueryAsync(userInput);

                // Add AI message to history
                CoreServices.History.AddEntry(_conversationId, new ConversationEntry { ParticipantId = AiId, Role = "assistant", Content = aiResponse, GameTicksTimestamp = CoreServices.SafeAccessService.GetTicksGameSafe() });

                // Update UI with AI response
                thinkingMessage.Content = aiResponse; // Replace "..." with actual response
            }
            catch (System.Exception ex)
            {
                thinkingMessage.Content = $"Error: {ex.Message}";
                Log.Error($"[MainTabWindow_RimAI] Error handling send: {ex}");
            }
            finally
            {
                _isProcessing = false;
                ScrollToBottom();
            }
        }
        
        private void ScrollToBottom()
        {
            // This ensures the scroll view jumps to the latest message
            _scrollPosition.y = CalculateHistoryHeight();
        }
    }
}
