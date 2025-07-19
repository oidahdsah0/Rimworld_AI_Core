using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Framework.API;
using RimAI.Core.UI;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Text;

namespace RimAI.Core.UI
{
    public class MainTabWindow_RimAI : MainTabWindow
    {
        private string inputText = "";
        private string responseText = "";
        private bool isProcessing = false;
        private Vector2 scrollPosition = Vector2.zero;
        private StringBuilder streamingResponse = new StringBuilder();
        private bool isStreaming = false;
        private float lastUpdateTime = 0f;
        
        // 添加取消支持
        private CancellationTokenSource currentCancellationTokenSource = null;
        
        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("🤖 RimAI Control Panel | RimAI 控制面板");
            
            // 显示当前模式状态
            if (RimAIApi.IsStreamingEnabled())
            {
                listingStandard.Label("🚀 Fast Response Mode Enabled | 快速响应模式已启用");
            }
            else
            {
                listingStandard.Label("📝 Standard Response Mode | 标准响应模式");
            }
            
            listingStandard.Gap();
            
            // 添加输入框标签
            listingStandard.Label("Enter Command | 输入指令:");
            
            // 添加输入框
            Rect textFieldRect = listingStandard.GetRect(30f);
            inputText = Widgets.TextField(textFieldRect, inputText);
            
            listingStandard.Gap();
            
            // 添加确认按钮
            string buttonText = isProcessing ? 
                (isStreaming ? "Receiving Response... | 接收响应中..." : "Processing... | 处理中...") : 
                "Send to AI | 发送给AI";
            
            if (listingStandard.ButtonText(buttonText))
            {
                if (string.IsNullOrWhiteSpace(inputText))
                {
                    Messages.Message("Please enter valid command content | 请输入有效的命令内容", MessageTypeDefOf.RejectInput, false);
                }
                else if (!isProcessing)
                {
                    ProcessAIRequest();
                }
            }
            
            // 添加取消按钮（仅在处理时显示）
            if (isProcessing && listingStandard.ButtonText("❌ Cancel Request | 取消请求"))
            {
                CancelCurrentRequest();
            }
            
            // 添加高级AI助手按钮
            if (listingStandard.ButtonText("🚀 Open Advanced AI Assistant | 打开高级AI助手"))
            {
                Find.WindowStack.Add(new Dialog_AdvancedAIAssistant());
            }
            
            listingStandard.Gap();
            
            // 显示AI响应
            if (!string.IsNullOrEmpty(responseText) || (isStreaming && streamingResponse.Length > 0))
            {
                listingStandard.Label("AI Response | AI响应:");
                
                string displayText = isStreaming ? streamingResponse.ToString() : responseText;
                
                // 创建一个可滚动的文本区域
                Rect responseRect = listingStandard.GetRect(200f);
                Rect viewRect = new Rect(0f, 0f, responseRect.width - 16f, Text.CalcHeight(displayText, responseRect.width));
                
                Widgets.BeginScrollView(responseRect, ref scrollPosition, viewRect);
                Widgets.Label(viewRect, displayText);
                Widgets.EndScrollView();
                
                // 流式模式下显示光标效果
                if (isStreaming && Time.unscaledTime - lastUpdateTime < 0.5f)
                {
                    var cursorRect = new Rect(viewRect.x + Text.CalcSize(displayText).x, viewRect.y, 20f, 20f);
                    Widgets.Label(cursorRect, "_");
                }
            }
            
            listingStandard.End();
        }
        
        private async void ProcessAIRequest()
        {
            isProcessing = true;
            streamingResponse.Clear();
            
            // 创建新的取消令牌源
            currentCancellationTokenSource?.Cancel();
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // 构建完整的提示
                string prompt = $@"作为RimWorld殖民地的AI助手，请回答以下问题或执行以下指令：
{inputText}

重要限制：
- 仅提供游戏相关的建设性回答
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持友善、专业的游戏助手语调
- 返回语言要与用户所写内容一致";
                
                // 检查是否应该使用流式（UI界面适合实时更新）
                bool useStreaming = RimAIApi.IsStreamingEnabled();
                
                if (useStreaming)
                {
                    isStreaming = true;
                    responseText = "";
                    lastUpdateTime = Time.unscaledTime;
                    
                    await RimAIApi.GetChatCompletionStream(
                        prompt,
                        chunk =>
                        {
                            // 检查是否已取消
                            if (currentCancellationTokenSource?.IsCancellationRequested == true)
                                return;
                                
                            streamingResponse.Append(chunk);
                            lastUpdateTime = Time.unscaledTime;
                            // UI会在下一帧自动更新
                        },
                        currentCancellationTokenSource?.Token ?? CancellationToken.None
                    );
                    
                    if (currentCancellationTokenSource?.IsCancellationRequested != true)
                    {
                        responseText = streamingResponse.ToString();
                        Messages.Message("AI response completed! | AI响应完成!", MessageTypeDefOf.PositiveEvent);
                    }
                    isStreaming = false;
                }
                else
                {
                    responseText = "Processing request...";
                    
                    string aiResponse = await RimAIApi.GetChatCompletion(prompt, currentCancellationTokenSource?.Token ?? CancellationToken.None);
                    
                    if (currentCancellationTokenSource?.IsCancellationRequested != true)
                    {
                        if (!string.IsNullOrEmpty(aiResponse))
                        {
                            responseText = aiResponse;
                            Messages.Message("AI response received successfully! | AI响应接收成功!", MessageTypeDefOf.PositiveEvent);
                        }
                        else
                        {
                            responseText = "Error: No response from AI service.";
                            Messages.Message("Failed to get AI response. | 获取AI响应失败。", MessageTypeDefOf.NegativeEvent);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                responseText = "请求已被用户取消";
                Messages.Message("AI request cancelled | AI请求已取消", MessageTypeDefOf.NeutralEvent);
                Log.Message("[MainTabWindow_RimAI] Request was cancelled by user");
            }
            catch (Exception ex)
            {
                responseText = $"Error: {ex.Message}";
                Log.Error($"RimAI API call failed: {ex.Message}");
                Messages.Message($"AI request failed | AI请求失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
            finally
            {
                isProcessing = false;
                isStreaming = false;
                currentCancellationTokenSource?.Dispose();
                currentCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 取消当前请求
        /// </summary>
        private void CancelCurrentRequest()
        {
            if (currentCancellationTokenSource != null && !currentCancellationTokenSource.IsCancellationRequested)
            {
                currentCancellationTokenSource.Cancel();
                Log.Message("[MainTabWindow_RimAI] User cancelled current request");
            }
        }

        /// <summary>
        /// 关闭窗口时的清理工作
        /// </summary>
        public override void PostClose()
        {
            // 取消所有待处理的请求
            CancelCurrentRequest();
            base.PostClose();
        }
    }
}
