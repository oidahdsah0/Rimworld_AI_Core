using System;

namespace RimAI.Core.Source.Infrastructure.Diagnostics
{
    public sealed class ServiceHealth
    {
        public string ServiceName { get; }
        public bool IsOk { get; }
        public TimeSpan ConstructionElapsed { get; }
        public string ErrorMessage { get; }

        public ServiceHealth(string serviceName, bool isOk, TimeSpan elapsed, string errorMessage)
        {
            ServiceName = serviceName;
            IsOk = isOk;
            ConstructionElapsed = elapsed;
            ErrorMessage = errorMessage;
        }
    }
}


