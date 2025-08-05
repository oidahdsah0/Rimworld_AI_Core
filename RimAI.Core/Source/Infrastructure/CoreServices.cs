using RimAI.Core.Infrastructure;

namespace RimAI.Core
{
    /// <summary>
    /// Restricted static service locator for scenarios where constructor injection is impossible.
    /// Use ONLY in UI/GameComponent contexts.
    /// </summary>
    public static class CoreServices
    {
        // Add new services as needed; keep usage minimal per architecture guidelines.

        public static T Resolve<T>() => ServiceContainer.Resolve<T>();
    }
}
