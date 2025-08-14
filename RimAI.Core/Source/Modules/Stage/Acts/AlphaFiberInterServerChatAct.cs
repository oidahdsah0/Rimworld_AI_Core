using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
	internal sealed class AlphaFiberInterServerChatAct : IStageAct
	{
		private readonly ILLMService _llm;
		private readonly IWorldDataService _world;

		public AlphaFiberInterServerChatAct(ILLMService llm, IWorldDataService world)
		{
			_llm = llm;
			_world = world;
		}

		public string Name => "AlphaFiberInterServerChat";

		public bool IsEligible(StageExecutionRequest req)
		{
			var n = req?.Ticket?.ParticipantIds?.Count ?? 0;
			return n == 2;
		}

		public async Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct)
		{
			try
			{
				var conv = "stage-" + (req?.Ticket?.ConvKey ?? Guid.NewGuid().ToString("N"));
				var a = req?.Ticket?.ParticipantIds?.ElementAtOrDefault(0) ?? "thing:serverA";
				var b = req?.Ticket?.ParticipantIds?.ElementAtOrDefault(1) ?? "thing:serverB";
				var system = "你是 RimAI 的系统级诊断器。";
				var user = $"基于服务器状态生成150-300字的两台AI服务器对话总结。服务器对：{a} 与 {b}。场景：{(req?.ScenarioText ?? "(无)")}";
				var r = await _llm.GetResponseAsync(conv, system, user, ct);
				if (!r.IsSuccess) return new ActResult { Completed = false, Reason = "LLMError", FinalText = "（本轮对话失败或超时，已跳过）" };
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


