using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.LLM
{
	internal static class LlmPolicies
	{
		private sealed class CircuitState
		{
			public int WindowMs;
			public int CooldownMs;
			public double ErrorThreshold;
			public int Failures;
			public int Total;
			public CircuitStatus Status = CircuitStatus.Closed;
			public DateTime LastTransitionUtc = DateTime.UtcNow;
		}

		private enum CircuitStatus { Closed, Open, HalfOpen }

		private static readonly ConcurrentDictionary<string, CircuitState> _circuits = new ConcurrentDictionary<string, CircuitState>();

		public static async Task<T> ExecuteWithRetryAsync<T>(
			Func<CancellationToken, Task<T>> operation,
			int maxAttempts,
			int baseDelayMs,
			Func<T, bool> isTransientFailure,
			CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				attempt++;
				try
				{
					var outcome = await operation(cancellationToken).ConfigureAwait(false);
					// 如果不是可重试失败，或已到达最大重试次数，则返回结果
					if (!isTransientFailure(outcome) || attempt >= maxAttempts)
					{
						return outcome;
					}
				}
				catch (Exception)
				{
					// 抛出的异常视为可重试的瞬态错误；若已到上限则抛出
					if (attempt >= maxAttempts)
					{
						throw;
					}
				}

				var delay = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		public static bool IsAllowedByCircuit(string key, int windowMs, int cooldownMs, double errorThreshold)
		{
			var state = _circuits.GetOrAdd(key, _ => new CircuitState
			{
				WindowMs = windowMs,
				CooldownMs = cooldownMs,
				ErrorThreshold = errorThreshold
			});

			if (state.Status == CircuitStatus.Open)
			{
				if ((DateTime.UtcNow - state.LastTransitionUtc).TotalMilliseconds >= state.CooldownMs)
				{
					state.Status = CircuitStatus.HalfOpen;
					state.LastTransitionUtc = DateTime.UtcNow;
					return true; // allow a probe
				}
				return false;
			}
			return true;
		}

		public static void RecordResult(string key, bool isSuccess)
		{
			if (!_circuits.TryGetValue(key, out var state))
			{
				return;
			}
			// Simple rolling window using decay
			var now = DateTime.UtcNow;
			var elapsedMs = (now - state.LastTransitionUtc).TotalMilliseconds;
			if (elapsedMs > state.WindowMs)
			{
				state.Failures = 0;
				state.Total = 0;
				state.LastTransitionUtc = now;
			}
			state.Total++;
			if (!isSuccess) state.Failures++;
			var errorRate = state.Total == 0 ? 0.0 : (double)state.Failures / state.Total;
			if (state.Status == CircuitStatus.Closed && errorRate > state.ErrorThreshold)
			{
				state.Status = CircuitStatus.Open;
				state.LastTransitionUtc = now;
			}
			else if (state.Status == CircuitStatus.HalfOpen)
			{
				state.Status = isSuccess ? CircuitStatus.Closed : CircuitStatus.Open;
				state.LastTransitionUtc = now;
			}
		}
	}
}


