namespace RimAI.Core.Source.Modules.Stage.Diagnostics
{
	internal interface IStageLogging
	{
		void Info(string message);
		void Warn(string message);
		void Error(string message);
	}
}


