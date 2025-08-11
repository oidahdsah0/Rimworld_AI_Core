using System;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 个人观点与意识形态模型。
    /// 表达个体稳定的内在性格/价值取向，作为提示组装中最稳定的基础层。
    /// </summary>
    internal sealed class PersonalBeliefs : IEquatable<PersonalBeliefs>
    {
        public string Worldview { get; }
        public string Values { get; }
        public string CodeOfConduct { get; }
        public string TraitsText { get; }

        public PersonalBeliefs(string worldview, string values, string codeOfConduct, string traitsText)
        {
            Worldview = worldview ?? string.Empty;
            Values = values ?? string.Empty;
            CodeOfConduct = codeOfConduct ?? string.Empty;
            TraitsText = traitsText ?? string.Empty;
        }

        public bool Equals(PersonalBeliefs other)
        {
            if (other is null) return false;
            return string.Equals(Worldview, other.Worldview, StringComparison.Ordinal)
                && string.Equals(Values, other.Values, StringComparison.Ordinal)
                && string.Equals(CodeOfConduct, other.CodeOfConduct, StringComparison.Ordinal)
                && string.Equals(TraitsText, other.TraitsText, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 个人观点与意识形态服务。
    /// 主存以 pawnId → PersonalBeliefs 存储，并提供快照导入/导出以配合持久化层。
    /// </summary>
    internal interface IPersonalBeliefsAndIdeologyService
    {
        PersonalBeliefs GetByPawn(string pawnId);
        void UpsertByPawn(string pawnId, PersonalBeliefs beliefs);
        bool DeleteByPawn(string pawnId);

        IReadOnlyDictionary<string, PersonalBeliefs> ExportSnapshot();
        void ImportSnapshot(IReadOnlyDictionary<string, PersonalBeliefs> snapshot);
    }
}


