using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona
{
	internal interface IPersonaAutoSettingsService
	{
		bool GetAutoBio(string entityId);
		bool GetAutoIdeo(string entityId);
		void SetAutoBio(string entityId, bool enabled);
		void SetAutoIdeo(string entityId, bool enabled);
		int GetLastRunDay();
		void SetLastRunDay(int day);
		int GetIntervalDays();
		void SetIntervalDays(int days);
		int GetPerPawnDelayMs();
		void SetPerPawnDelayMs(int delayMs);
		int GetMaxRetries();
		void SetMaxRetries(int retries);
	}
}


