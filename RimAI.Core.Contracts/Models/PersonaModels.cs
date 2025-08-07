using System;
using System.Collections.Generic;

namespace RimAI.Core.Contracts.Models
{
    /// <summary>
    /// 表示一个 AI 人格（Persona）。
    /// Name 作为唯一键，用于在 UI 与调用链中标识人格。
    /// SystemPrompt 是传递给 LLM 的系统提示词，用以控制回复风格。
    /// Traits 预留给未来的附加属性（如语气、兴趣爱好等），当前可为空。
    /// 此类型为不可变（只读属性 + 构造函数注入），确保持久化与多线程安全。
    /// </summary>
    public sealed class Persona
    {
        public string Name { get; }
        public string SystemPrompt { get; }
        public IReadOnlyDictionary<string, string> Traits { get; }

        public Persona(string name, string systemPrompt, IReadOnlyDictionary<string, string> traits = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Persona name cannot be null or whitespace.", nameof(name));
            if (string.IsNullOrWhiteSpace(systemPrompt))
                throw new ArgumentException("SystemPrompt cannot be null or whitespace.", nameof(systemPrompt));

            Name = name;
            SystemPrompt = systemPrompt;
            Traits = traits ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// PersonaService 用于持久化的快照结构，内部仅保留一个 Name->Persona 的只读字典。
    /// </summary>
    public sealed class PersonaState
    {
        public IReadOnlyDictionary<string, Persona> Personas { get; }

        public PersonaState(IReadOnlyDictionary<string, Persona> personas)
        {
            Personas = personas ?? throw new ArgumentNullException(nameof(personas));
        }
    }
}
