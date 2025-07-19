using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using RimAI.Core.AI;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace RimAI.Core.UI
{
    /// <summary>
    /// 高级AI助手对话窗口
    /// 展示流式API的高级用法和适配不同响应模式的UI设计
    /// </summary>
    public class Dialog_AdvancedAIAssistant : Window
    {
        private string inputText = "";
        private StringBuilder conversationHistory = new StringBuilder();
        private StringBuilder currentResponse = new StringBuilder();
        private Vector2 conversationScrollPos = Vector2.zero;
        private bool isProcessing = false;
        private bool isStreaming = false;
        private float typingEffectTimer = 0f;
        private string pendingInput = "";
        
        // 增加取消支持
        private CancellationTokenSource currentCancellationTokenSource = null;
        
        public override Vector2 InitialSize => new Vector2(1200f, 900f);
        
        public override bool IsDebug => false;

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            // 设置窗体属性
            this.closeOnClickedOutside = true;
            this.draggable = true;
            this.resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect contentRect = inRect;
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);

            // 标题和状态
            Text.Font = GameFont.Medium;
            listing.Label("🤖 RimWorld AI Assistant | RimWorld AI助手");
            Text.Font = GameFont.Small;
            
            // 显示服务状态
            var statusInfo = SmartGovernor.Instance.GetServiceStatus();
            var statusLines = statusInfo.Split('\n');
            foreach (var line in statusLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    listing.Label(line.Trim());
                }
            }
            
            listing.Gap();

            // 对话历史区域
            listing.Label("Conversation History | 对话历史:");
            Rect conversationRect = listing.GetRect(550f);
            DrawConversationArea(conversationRect);

            listing.Gap();

            // 输入区域
            listing.Label("Input Message | 输入消息:");
            Rect inputRect = listing.GetRect(60f);
            inputText = Widgets.TextArea(inputRect, inputText);

            listing.Gap();

            // 按钮区域 - 水平排列
            DrawButtons(listing);

            listing.End();

            // 更新打字机效果
            if (isStreaming)
            {
                typingEffectTimer += Time.unscaledDeltaTime;
            }
        }

        private void DrawConversationArea(Rect rect)
        {
            string displayText = conversationHistory.ToString();
            
            // 如果正在流式接收，添加当前响应
            if (isStreaming && currentResponse.Length > 0)
            {
                // 根据当前正在处理的类型显示不同的前缀
                string prefix = conversationHistory.ToString().Contains("👤 请求: 总督") ? "🏛️ 总督: " : "🤖 AI: ";
                displayText += $"\n{prefix}{currentResponse}";
                
                // 添加打字机光标效果
                if (typingEffectTimer % 1f < 0.5f)
                {
                    displayText += "_";
                }
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, Text.CalcHeight(displayText, rect.width));
            
            Widgets.BeginScrollView(rect, ref conversationScrollPos, viewRect);
            Widgets.Label(viewRect, displayText);
            Widgets.EndScrollView();

            // 自动滚动到底部
            if (isStreaming || !string.IsNullOrEmpty(pendingInput))
            {
                conversationScrollPos.y = Mathf.Max(0f, viewRect.height - rect.height);
            }
        }

        private void DrawButtons(Listing_Standard listing)
        {
            // 获取一行的高度用于按钮
            Rect buttonRowRect = listing.GetRect(35f);
            
            // 按钮参数
            float buttonHeight = 35f;
            float buttonSpacing = 5f;
            
            // 计算按钮数量
            List<ButtonData> buttons = new List<ButtonData>();
            
            // 准备按钮数据
            string sendButtonText = isProcessing ? "Processing... | 处理中..." : "Send | 发送";
            bool sendEnabled = !string.IsNullOrWhiteSpace(inputText) && !isProcessing;
            buttons.Add(new ButtonData(sendButtonText, () => SendMessage(inputText, false), sendEnabled));
            
            buttons.Add(new ButtonData("🏛️Governor | 总督", () => GetGovernorAdvice(), !isProcessing));
            buttons.Add(new ButtonData("Clear | 清空", () => ClearConversation(), true));
            buttons.Add(new ButtonData("Close | 关闭", () => Close(), true));
            
            if (isProcessing)
            {
                buttons.Add(new ButtonData("❌Cancel | 取消", () => CancelCurrentRequest(), true));
            }
            
            // 计算按钮宽度
            float totalSpacing = (buttons.Count - 1) * buttonSpacing;
            float buttonWidth = (buttonRowRect.width - totalSpacing) / buttons.Count;
            
            // 绘制按钮
            float currentX = buttonRowRect.x;
            foreach (var button in buttons)
            {
                Rect buttonRect = new Rect(currentX, buttonRowRect.y, buttonWidth, buttonHeight);
                
                bool prevEnabled = GUI.enabled;
                GUI.enabled = button.enabled;
                
                if (Widgets.ButtonText(buttonRect, button.text))
                {
                    button.action?.Invoke();
                }
                
                GUI.enabled = prevEnabled;
                
                currentX += buttonWidth + buttonSpacing;
            }
        }
        
        // 按钮数据结构
        private struct ButtonData
        {
            public string text;
            public System.Action action;
            public bool enabled;
            
            public ButtonData(string text, System.Action action, bool enabled)
            {
                this.text = text;
                this.action = action;
                this.enabled = enabled;
            }
        }
        
        /// <summary>
        /// 统一的流式处理函数
        /// </summary>
        private async Task HandleStreamingOrStandardResponse(string prompt, string displayPrefix, string successMessage, string fallbackThinkingText = null)
        {
            bool useStreaming = RimAIAPI.IsStreamingEnabled;
            
            if (useStreaming)
            {
                // 流式处理
                isStreaming = true;
                currentResponse.Clear();
                typingEffectTimer = 0f;
                
                await RimAIAPI.SendStreamingMessageAsync(
                    prompt,
                    chunk =>
                    {
                        // 检查是否已经取消
                        if (currentCancellationTokenSource?.IsCancellationRequested == true)
                            return;
                            
                        currentResponse.Append(chunk);
                        typingEffectTimer = 0f; // 重置光标闪烁
                    },
                    currentCancellationTokenSource?.Token ?? CancellationToken.None
                );
                
                // 将完整响应添加到历史
                if (currentCancellationTokenSource?.IsCancellationRequested != true)
                {
                    conversationHistory.AppendLine($"\n{displayPrefix}: {currentResponse}");
                    Messages.Message(successMessage, MessageTypeDefOf.PositiveEvent, false);
                }
                isStreaming = false;
            }
            else
            {
                // 标准处理
                if (!string.IsNullOrEmpty(fallbackThinkingText))
                {
                    conversationHistory.AppendLine($"\n{fallbackThinkingText}");
                }
                
                var response = await RimAIAPI.SendMessageAsync(prompt, currentCancellationTokenSource?.Token ?? CancellationToken.None);
                
                // 移除思考中的文本（如果有）
                if (!string.IsNullOrEmpty(fallbackThinkingText))
                {
                    var historyText = conversationHistory.ToString();
                    var lastThinkingIndex = historyText.LastIndexOf(fallbackThinkingText);
                    if (lastThinkingIndex >= 0)
                    {
                        conversationHistory.Clear();
                        conversationHistory.Append(historyText.Substring(0, lastThinkingIndex));
                    }
                }
                
                if (!string.IsNullOrEmpty(response) && currentCancellationTokenSource?.IsCancellationRequested != true)
                {
                    conversationHistory.AppendLine($"{displayPrefix}: {response}");
                    Messages.Message(successMessage, MessageTypeDefOf.PositiveEvent, false);
                }
                else if (currentCancellationTokenSource?.IsCancellationRequested != true)
                {
                    conversationHistory.AppendLine($"{displayPrefix}: Sorry, unable to generate response. | 抱歉，无法生成响应。");
                    Messages.Message("响应失败", MessageTypeDefOf.NegativeEvent, false);
                }
            }
        }

        private async void SendMessage(string message, bool forceQuickResponse = false)
        {
            if (string.IsNullOrWhiteSpace(message)) 
            {
                Messages.Message("请输入有效消息内容", MessageTypeDefOf.RejectInput, false);
                return;
            }

            isProcessing = true;
            pendingInput = message;
            
            // 创建新的取消令牌
            currentCancellationTokenSource?.Cancel();
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = new CancellationTokenSource();
            
            // 添加用户消息到对话历史
            conversationHistory.AppendLine($"\n👤 You | 你: {message}");
            
            // 清空输入框
            inputText = "";

            try
            {
                var prompt = $@"作为RimWorld AI助手，请回答：{message}

重要限制：
- 仅提供游戏相关的建设性回答
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持友善、专业的游戏助手语调
- 返回语言要与用户所写内容一致";
                await HandleStreamingOrStandardResponse(prompt, "🤖 AI", "AI response completed | AI响应完成", "🤖 AI: Thinking... | 正在思考...");
            }
            catch (OperationCanceledException)
            {
                conversationHistory.AppendLine("\n❌ Request cancelled by user | 请求已被用户取消");
            }
            catch (Exception ex)
            {
                conversationHistory.AppendLine($"\n❌ Error | 错误: {ex.Message}");
                Log.Error($"[AdvancedAIAssistant] Message failed: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                isStreaming = false;
                pendingInput = "";
                currentResponse.Clear();
                currentCancellationTokenSource?.Dispose();
                currentCancellationTokenSource = null;
            }
        }

        private async void GetGovernorAdvice()
        {
            isProcessing = true;
            
            // 创建新的取消令牌
            currentCancellationTokenSource?.Cancel();
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = new CancellationTokenSource();
            
            var colonyStatus = GetBasicColonyStatus();
            
            conversationHistory.AppendLine($"\n👤 Request | 请求: Governor, please analyze current colony status | 总督，请分析当前殖民地状况");
            
            try
            {
                if (RimAIAPI.IsStreamingEnabled)
                {
                    // 流式模式：使用快速决策提示词
                    var prompt = $@"作为RimWorld殖民地紧急管理AI，请对以下殖民地状况提供简明扼要的应对建议（不超过100字）：
{colonyStatus}

重要限制：
- 仅提供游戏内管理建议
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致";
                    await HandleStreamingOrStandardResponse(prompt, "🏛️ Governor(Fast) | 总督(快速)", "Governor quick suggestion generated | 总督快速建议已生成");
                }
                else
                {
                    // 标准模式：使用详细策略
                    conversationHistory.AppendLine("\n🏛️ Governor | 总督: Analyzing in detail... | 正在详细分析...");
                    var advice = await SmartGovernor.Instance.GetDetailedStrategy(colonyStatus, currentCancellationTokenSource.Token);
                    
                    // 移除"正在详细分析..."
                    var historyText = conversationHistory.ToString();
                    var lastThinkingIndex = historyText.LastIndexOf("🏛️ Governor | 总督: Analyzing in detail... | 正在详细分析...");
                    if (lastThinkingIndex >= 0)
                    {
                        conversationHistory.Clear();
                        conversationHistory.Append(historyText.Substring(0, lastThinkingIndex));
                    }
                    
                    if (!string.IsNullOrEmpty(advice) && currentCancellationTokenSource?.IsCancellationRequested != true)
                    {
                        conversationHistory.AppendLine($"🏛️ Governor(Detailed) | 总督(详细): {advice}");
                        Messages.Message("Governor detailed suggestion generated | 总督详细建议已生成", MessageTypeDefOf.PositiveEvent, false);
                    }
                    else if (string.IsNullOrEmpty(advice) && currentCancellationTokenSource?.IsCancellationRequested != true)
                    {
                        conversationHistory.AppendLine("🏛️ Governor | 总督: Sorry, unable to generate suggestion. | 抱歉，无法生成建议。");
                        Messages.Message("Governor suggestion failed | 总督建议失败", MessageTypeDefOf.NegativeEvent, false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                conversationHistory.AppendLine("\n❌ Governor suggestion cancelled | 总督建议已被取消");
            }
            catch (Exception ex)
            {
                conversationHistory.AppendLine($"\n❌ Governor suggestion failed | 总督建议失败: {ex.Message}");
                Log.Error($"[AdvancedAIAssistant] Governor advice failed: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                isStreaming = false;
                currentResponse.Clear();
                currentCancellationTokenSource?.Dispose();
                currentCancellationTokenSource = null;
            }
        }

        private void ClearConversation()
        {
            conversationHistory.Clear();
            currentResponse.Clear();
            conversationScrollPos = Vector2.zero;
            conversationHistory.AppendLine("🤖 AI Assistant ready, how can I help you? | AI助手已就绪，有什么可以帮助您的吗？");
        }

        /// <summary>
        /// 取消当前请求
        /// </summary>
        private void CancelCurrentRequest()
        {
            if (currentCancellationTokenSource != null && !currentCancellationTokenSource.IsCancellationRequested)
            {
                currentCancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// 关闭窗口时的清理工作
        /// </summary>
        public override void Close(bool doCloseSound = true)
        {
            // 取消所有待处理的请求
            CancelCurrentRequest();
            base.Close(doCloseSound);
        }

        private string GetBasicColonyStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return "没有当前地图";

            var pawns = map.mapPawns.ColonistsSpawnedCount;
            var prisoners = map.mapPawns.PrisonersOfColonySpawnedCount;
            
            return $"殖民者数量: {pawns}, 囚犯数量: {prisoners}";
        }
    }
}
