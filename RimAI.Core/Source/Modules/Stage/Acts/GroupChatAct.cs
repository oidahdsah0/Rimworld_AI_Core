using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
	internal sealed class GroupChatAct : IStageAct
	{
		private readonly ILLMService _llm;

		public GroupChatAct(ILLMService llm)
		{
			_llm = llm;
		}

		public string Name => "GroupChat";

		public bool IsEligible(StageExecutionRequest req)
		{
			var n = req?.Ticket?.ParticipantIds?.Count ?? 0;
			return n >= 2;
		}

		public async Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct)
		{
			try
			{
				var conv = "stage-" + (req?.Ticket?.ConvKey ?? Guid.NewGuid().ToString("N"));
				var participants = string.Join(", ", req?.Ticket?.ParticipantIds ?? Array.Empty<string>());
				var system = "你是 RimAI 的系统级总结器。";
				var user = $"请用{(req?.Locale ?? "zh-Hans")}写一段150-300字的群聊总结。参与者：{participants}。场景：{(req?.ScenarioText ?? "(无)")}";
				var r = await _llm.GetResponseAsync(conv, system, user, ct);
				if (!r.IsSuccess)
				{
					return new ActResult { Completed = false, Reason = "LLMError", FinalText = "（本轮对话失败或超时，已跳过）" };
				}
				var text = r.Value?.Message?.Content ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text)) text = "（本轮对话失败或超时，已跳过）";
				return new ActResult { Completed = true, Reason = "Completed", FinalText = text, Rounds = 1 };
			}
			catch (OperationCanceledException)
			{
				return new ActResult { Completed = false, Reason = "Timeout", FinalText = "（本轮对话失败或超时，已跳过）" };
			}
			catch (Exception)
			{
				return new ActResult { Completed = false, Reason = "Exception", FinalText = "（本轮对话失败或超时，已跳过）" };
			}
		}

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;
	}
}


