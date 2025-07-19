using RimAI.Framework.API;
using RimWorld;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Verse;

namespace RimAI.Core.AI
{
    /// <summary>
    /// 智能总督 - 根据殖民地状态提供管理建议
    /// 展示如何在不同场景下使用流式和非流式API
    /// </summary>
    public class SmartGovernor
    {
        private static SmartGovernor _instance;
        public static SmartGovernor Instance => _instance ??= new SmartGovernor();
        
        // 为长期任务添加取消支持
        private CancellationTokenSource _currentOperationCts;

        /// <summary>
        /// 获取快速决策建议（适合实时场景，如紧急事件）
        /// 使用流式API提供快速响应
        /// </summary>
        public async Task<string> GetQuickDecision(string situation, CancellationToken cancellationToken = default)
        {
            // 创建操作级别的取消令牌源
            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();
            _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                if (!RimAIApi.IsStreamingEnabled())
                {
                    // 如果用户禁用了流式，直接使用标准API
                    return await GetStandardDecision(situation, _currentOperationCts.Token);
                }

                var prompt = $@"作为RimWorld殖民地紧急管理AI，请对以下紧急情况提供简明扼要的应对建议（不超过100字）：
{situation}

重要限制：
- 仅提供游戏内管理建议
- 不得生成NSFW、暴力、政治敏感等不当内容  
- 不得讨论现实世界敏感话题
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致";
                
                // 使用GetChatCompletionWithOptions强制启用流式
                var response = await RimAIApi.GetChatCompletionWithOptions(prompt, forceStreaming: true, _currentOperationCts.Token);
                
                Log.Message($"[SmartGovernor] Quick decision provided for: {situation}");
                return response ?? "无法获取快速决策建议";
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Quick decision was cancelled");
                return "决策已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Quick decision failed: {ex.Message}");
                return $"决策失败: {ex.Message}";
            }
            finally
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
            }
        }

        /// <summary>
        /// 获取详细的管理策略（适合后台分析，不需要实时反馈）
        /// 使用标准API进行深度思考
        /// </summary>
        public async Task<string> GetDetailedStrategy(string colonyStatus, CancellationToken cancellationToken = default)
        {
            // 创建操作级别的取消令牌源
            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();
            _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var prompt = $@"作为RimWorld殖民地管理专家，请根据以下殖民地状态制定详细的管理策略和优先事项：
{colonyStatus}

请提供：
1. 当前状况分析
2. 优先处理事项
3. 中长期发展建议
4. 风险预警

重要限制：
- 仅提供游戏内策略建议
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致";
                
                // 对于详细分析，我们不强制流式，让Framework根据设置决定
                var response = await RimAIApi.GetChatCompletion(prompt, _currentOperationCts.Token);
                
                Log.Message("[SmartGovernor] Detailed strategy generated");
                return response ?? "无法生成详细策略";
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Detailed strategy was cancelled");
                return "策略生成已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Detailed strategy failed: {ex.Message}");
                return $"策略生成失败: {ex.Message}";
            }
            finally
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
            }
        }

        /// <summary>
        /// 标准决策方法（向后兼容）
        /// </summary>
        private async Task<string> GetStandardDecision(string situation, CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = $@"作为RimWorld殖民地管理AI，请对以下情况提供管理建议：
{situation}

重要限制：
- 仅提供游戏内管理建议
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题  
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致";
                var response = await RimAIApi.GetChatCompletion(prompt, cancellationToken);
                
                return response ?? "无法获取管理建议";
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Standard decision was cancelled");
                return "决策已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Standard decision failed: {ex.Message}");
                return $"决策失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取实时解说（用于事件发生时的流式解说）
        /// 展示流式API的实时反馈能力
        /// </summary>
        public async Task<string> GetEventNarration(string eventDescription, Action<string> onPartialNarration = null, CancellationToken cancellationToken = default)
        {
            // 创建操作级别的取消令牌源
            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();
            _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                if (!RimAIApi.IsStreamingEnabled() || onPartialNarration == null)
                {
                    // 如果不支持流式或没有回调，使用标准方法
                    var prompt = $@"作为RimWorld事件解说员，请生动描述以下事件：
{eventDescription}

重要限制：
- 仅描述游戏内事件和情况
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持生动有趣但适宜的游戏解说风格
- 返回语言要与用户所写内容一致";
                    return await RimAIApi.GetChatCompletion(prompt, _currentOperationCts.Token);
                }

                var streamPrompt = $@"作为专业的RimWorld事件解说员，请生动有趣地描述以下事件的发生过程：
{eventDescription}

重要限制：
- 仅描述游戏内事件和情况
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持生动有趣但适宜的游戏解说风格
- 返回语言要与用户所写内容一致";
                var fullNarration = new StringBuilder();
                
                await RimAIApi.GetChatCompletionStream(
                    streamPrompt,
                    chunk =>
                    {
                        if (_currentOperationCts.Token.IsCancellationRequested)
                            return;
                            
                        fullNarration.Append(chunk);
                        onPartialNarration?.Invoke(fullNarration.ToString());
                    },
                    _currentOperationCts.Token
                );

                var result = fullNarration.ToString();
                Log.Message($"[SmartGovernor] Event narration completed: {eventDescription}");
                
                return result;
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Event narration was cancelled");
                return "解说已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Event narration failed: {ex.Message}");
                return $"解说失败: {ex.Message}";
            }
            finally
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
            }
        }

        /// <summary>
        /// 取消当前正在进行的操作
        /// </summary>
        public void CancelCurrentOperation()
        {
            if (_currentOperationCts != null && !_currentOperationCts.IsCancellationRequested)
            {
                _currentOperationCts.Cancel();
                Log.Message("[SmartGovernor] Current operation cancelled by user");
            }
        }

        /// <summary>
        /// 获取当前AI服务状态信息
        /// </summary>
        public string GetServiceStatus()
        {
            var settings = RimAIApi.GetCurrentSettings();
            if (settings == null)
            {
                return "❌ AI服务未初始化";
            }

            var status = new StringBuilder();
            status.AppendLine("🤖 AI服务状态:");
            status.AppendLine($"模型: {settings.modelName}");
            status.AppendLine($"模式: {(RimAIApi.IsStreamingEnabled() ? "🚀 快速响应" : "📝 标准模式")}");
            status.AppendLine($"API端点: {settings.apiEndpoint}");
            
            return status.ToString();
        }
    }
}
