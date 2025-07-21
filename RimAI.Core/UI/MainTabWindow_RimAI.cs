using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using RimAI.Core.UI;
using RimAI.Core.Officers;
using RimAI.Core.Architecture;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Text;
using System.Linq;

namespace RimAI.Core.UI
{
    /// <summary>
    /// RimAI主标签窗口 - 指令下达和AI对话界面
    /// </summary>
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
        
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("🤖 RimAI Command Center | RimAI 指令中心");
            
            // 显示当前模式状态 - ✅ 修复：Framework v3.0 总是支持流式
            if (RimAIAPI.IsInitialized)
            {
                listingStandard.Label("🚀 Fast Response Mode Enabled | 快速响应模式已启用");
            }
            else
            {
                listingStandard.Label("📝 Framework Not Ready | 框架未就绪");
            }
            
            listingStandard.Gap();
            
            // 添加输入框标签
            listingStandard.Label("Enter Command | 输入指令:");
            
            // 添加输入框
            Rect textFieldRect = listingStandard.GetRect(60f);
            inputText = Widgets.TextArea(textFieldRect, inputText);
            
            listingStandard.Gap();
            
            // 按钮行
            DrawButtonRow(listingStandard, inRect.width);
            
            listingStandard.Gap();
            
            // 显示AI响应
            if (!string.IsNullOrEmpty(responseText) || (isStreaming && streamingResponse.Length > 0))
            {
                listingStandard.Label("AI Response | AI响应:");
                
                string displayText = isStreaming ? streamingResponse.ToString() : responseText;
                
                // 创建一个可滚动的文本区域
                Rect responseRect = listingStandard.GetRect(300f);
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

        private void DrawButtonRow(Listing_Standard listing, float availableWidth)
        {
            Rect buttonRowRect = listing.GetRect(35f);
            float buttonSpacing = 5f;
            
            // 计算按钮数量和宽度 - 添加总督按钮
            int buttonCount = isProcessing ? 4 : 3; // 发送给AI, 发送给总督, 设置, 取消(仅处理时)
            float totalSpacing = (buttonCount - 1) * buttonSpacing;
            float buttonWidth = (availableWidth - totalSpacing) / buttonCount;
            
            float currentX = buttonRowRect.x;
            
            // 发送给AI按钮
            string sendButtonText = isProcessing ? 
                (isStreaming ? "Receiving... | 接收中..." : "Processing... | 处理中...") : 
                "Send to AI | 发送给AI";
            
            Rect sendRect = new Rect(currentX, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            GUI.enabled = !string.IsNullOrWhiteSpace(inputText) && !isProcessing;
            
            if (Widgets.ButtonText(sendRect, sendButtonText))
            {
                ProcessAIRequest();
            }
            
            GUI.enabled = true;
            currentX += buttonWidth + buttonSpacing;
            
            // 总督按钮 - 新增！
            Rect governorRect = new Rect(currentX, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            GUI.enabled = !string.IsNullOrWhiteSpace(inputText) && !isProcessing;
            
            if (Widgets.ButtonText(governorRect, "🏛️ Governor | 总督"))
            {
                ProcessGovernorRequest();
            }
            
            GUI.enabled = true;
            currentX += buttonWidth + buttonSpacing;
            
            // 设置按钮
            Rect settingsRect = new Rect(currentX, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            if (Widgets.ButtonText(settingsRect, "⚙️ Settings | 设置"))
            {
                Find.WindowStack.Add(new Dialog_OfficerSettings());
            }
            
            currentX += buttonWidth + buttonSpacing;
            
            // 取消按钮（仅在处理时显示）
            if (isProcessing)
            {
                Rect cancelRect = new Rect(currentX, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                if (Widgets.ButtonText(cancelRect, "❌ Cancel | 取消"))
                {
                    CancelCurrentRequest();
                }
            }
        }
        
        private async void ProcessGovernorRequest()
        {
            // 🎯 防止重复处理
            if (isProcessing)
            {
                Log.Warning("[MainTabWindow_RimAI] Governor request already in progress, ignoring");
                return;
            }

            isProcessing = true;
            streamingResponse.Clear();
            
            // 🎯 安全地处理取消令牌源
            if (currentCancellationTokenSource != null)
            {
                try
                {
                    if (!currentCancellationTokenSource.IsCancellationRequested)
                    {
                        currentCancellationTokenSource.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已释放的令牌源
                }
                
                // 等待一小段时间让之前的操作完成
                await Task.Delay(50);
                
                try
                {
                    currentCancellationTokenSource?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已释放的令牌源
                }
            }
            
            // 创建新的令牌源
            currentCancellationTokenSource = new CancellationTokenSource();
            var token = currentCancellationTokenSource.Token;
            
            try
            {
                responseText = "正在咨询总督...";
                Log.Message("[MainTabWindow_RimAI] Consulting Governor via ServiceContainer");
                
                // 🎯 使用正确的服务容器模式！
                var governor = CoreServices.Governor;
                if (governor == null)
                {
                    responseText = "总督服务不可用 - 请检查服务容器配置";
                    Messages.Message("Governor service not available | 总督服务不可用", MessageTypeDefOf.NegativeEvent);
                    return;
                }
                
                // 🎯 新增：检查是否支持流式响应
                bool useStreaming = governor.IsAvailable && CoreServices.LLMService?.IsStreamingAvailable == true;
                string governorAdvice;
                
                if (useStreaming)
                {
                    // 🚀 使用流式响应 - 就像"发送给AI"按钮一样！
                    isStreaming = true;
                    responseText = "";
                    lastUpdateTime = Time.unscaledTime;
                    
                    if (!string.IsNullOrWhiteSpace(inputText))
                    {
                        // 有具体问题时，使用用户查询的流式处理方法
                        governorAdvice = await governor.HandleUserQueryStreamingAsync(
                            inputText,
                            chunk =>
                            {
                                // 检查是否已取消 - 使用局部变量token
                                if (token.IsCancellationRequested)
                                    return;
                                
                                // 🎯 修复：chunk已经是累积内容，直接设置而不是追加
                                streamingResponse.Clear();
                                streamingResponse.Append(chunk);
                                lastUpdateTime = Time.unscaledTime;
                                // UI会在下一帧自动更新
                            },
                            token
                        );
                    }
                    else
                    {
                        // 没有具体问题时，获取一般建议 + 流式
                        governorAdvice = await governor.GetStreamingAdviceAsync(
                            chunk =>
                            {
                                if (token.IsCancellationRequested)
                                    return;
                                
                                // 🎯 修复：chunk已经是累积内容，直接设置而不是追加
                                streamingResponse.Clear();
                                streamingResponse.Append(chunk);
                                lastUpdateTime = Time.unscaledTime;
                            },
                            token
                        );
                    }
                    
                    isStreaming = false;
                    
                    if (!token.IsCancellationRequested)
                    {
                        responseText = $"🏛️ 总督回复 (流式):\n\n{streamingResponse.ToString()}";
                        Messages.Message("Governor streaming consultation completed! | 总督流式咨询完成!", MessageTypeDefOf.PositiveEvent);
                    }
                }
                else
                {
                    // 🔄 回退到非流式模式
                    if (!string.IsNullOrWhiteSpace(inputText))
                    {
                        // 有具体问题时，使用用户查询处理方法
                        governorAdvice = await governor.HandleUserQueryAsync(inputText, token);
                    }
                    else
                    {
                        // 没有具体问题时，获取一般建议
                        governorAdvice = await governor.GetAdviceAsync(token);
                    }
                    
                    if (!token.IsCancellationRequested)
                    {
                        if (!string.IsNullOrEmpty(governorAdvice))
                        {
                            responseText = $"🏛️ 总督回复 (标准):\n\n{governorAdvice}";
                            Messages.Message("Governor consultation completed! | 总督咨询完成!", MessageTypeDefOf.PositiveEvent);
                            Log.Message("[MainTabWindow_RimAI] Governor advice received successfully via ServiceContainer");
                        }
                        else
                        {
                            responseText = "总督暂时无法提供建议";
                            Messages.Message("Governor unavailable | 总督暂时不可用", MessageTypeDefOf.NegativeEvent);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                responseText = "总督咨询已被取消";
                Messages.Message("Governor consultation cancelled | 总督咨询已取消", MessageTypeDefOf.NeutralEvent);
                Log.Message("[MainTabWindow_RimAI] Governor consultation was cancelled by user");
            }
            catch (Exception ex)
            {
                responseText = $"总督咨询失败: {ex.Message}";
                Log.Error($"[MainTabWindow_RimAI] Governor consultation failed: {ex.Message}");
                Messages.Message($"Governor consultation failed | 总督咨询失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
            finally
            {
                isProcessing = false;
                isStreaming = false;
                // 🎯 不在finally块中dispose令牌源
                // 让令牌源保持活跃状态，避免ObjectDisposedException
                // 只有在下次请求时才清理之前的令牌源
            }
        }
        
        private async void ProcessAIRequest()
        {
            // 🎯 防止重复处理
            if (isProcessing)
            {
                Log.Warning("[MainTabWindow_RimAI] AI request already in progress, ignoring");
                return;
            }

            isProcessing = true;
            streamingResponse.Clear();
            
            // 🎯 安全地处理取消令牌源
            var previousTokenSource = currentCancellationTokenSource;
            currentCancellationTokenSource = new CancellationTokenSource();
            
            // 在后台安全地清理之前的令牌源
            if (previousTokenSource != null)
            {
                try
                {
                    if (!previousTokenSource.IsCancellationRequested)
                    {
                        previousTokenSource.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已释放的令牌源
                }
                finally
                {
                    try
                    {
                        previousTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // 忽略已释放的令牌源
                    }
                }
            }
            
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
                
                // 检查是否应该使用流式（UI界面适合实时更新） - ✅ 修复：Framework v3.0 总是支持流式
                bool useStreaming = RimAIAPI.IsInitialized;
                
                if (useStreaming)
                {
                    isStreaming = true;
                    responseText = "";
                    lastUpdateTime = Time.unscaledTime;
                    
                    // ✅ 修复：正确的参数顺序 (prompt, onChunk, options, cancellationToken)
                    await RimAIAPI.SendStreamingMessageAsync(
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
                        null, // options
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
                    
                    // ✅ 修复：正确的参数顺序 (prompt, options, cancellationToken)
                    string aiResponse = await RimAIAPI.SendMessageAsync(
                        prompt, 
                        null, // options
                        currentCancellationTokenSource?.Token ?? CancellationToken.None
                    );
                    
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
                // 🎯 安全地释放当前的令牌源
                if (currentCancellationTokenSource != null)
                {
                    try
                    {
                        currentCancellationTokenSource.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // 忽略已释放的令牌源
                    }
                    finally
                    {
                        currentCancellationTokenSource = null;
                    }
                }
            }
        }

        /// <summary>
        /// 取消当前请求
        /// </summary>
        private void CancelCurrentRequest()
        {
            if (currentCancellationTokenSource != null)
            {
                try
                {
                    if (!currentCancellationTokenSource.IsCancellationRequested)
                    {
                        currentCancellationTokenSource.Cancel();
                        Log.Message("[MainTabWindow_RimAI] User cancelled current request");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 令牌源已被释放，忽略
                    Log.Message("[MainTabWindow_RimAI] Cancellation token source already disposed");
                }
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
