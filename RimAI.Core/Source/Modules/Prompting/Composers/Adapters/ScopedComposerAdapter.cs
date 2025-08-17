using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Adapters
{
	internal sealed class ScopedComposerAdapter : IPromptComposer, IProvidesUserPrefix
	{
		private readonly IPromptComposer _inner;
		private readonly PromptScope _scope;
		private readonly string _idOverride;
		private readonly int? _orderOverride;

		public ScopedComposerAdapter(IPromptComposer inner, PromptScope scope, string idOverride = null, int? orderOverride = null)
		{
			_inner = inner;
			_scope = scope;
			_idOverride = idOverride;
			_orderOverride = orderOverride;
		}

		public PromptScope Scope => _scope;
		public int Order => _orderOverride ?? _inner.Order;
		public string Id => string.IsNullOrWhiteSpace(_idOverride) ? _inner.Id : _idOverride;

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			return _inner.ComposeAsync(ctx, ct);
		}

		PromptScope IProvidesUserPrefix.Scope => _scope;

		public string GetUserPrefix(PromptBuildContext ctx)
		{
			if (_inner is IProvidesUserPrefix up)
			{
				return up.GetUserPrefix(ctx);
			}
			return string.Empty;
		}
	}
}


