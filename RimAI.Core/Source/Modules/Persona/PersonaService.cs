using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Models = RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 默认的人格管理服务 (P8)。
    /// 提供内存中的 CRUD 能力，并支持导入/导出状态以配合持久化层。
    /// </summary>
    internal sealed class PersonaService : IPersonaService
    {
        private const string DefaultName = "Default";
        private readonly ConcurrentDictionary<string, Models.Persona> _personas = new(StringComparer.OrdinalIgnoreCase);

        private static Models.Persona CreateDefaultPersona() => new Models.Persona(DefaultName, "你是一名友好且知识渊博的 RimWorld AI 助手，使用简体中文回答问题。");

        public PersonaService()
        {
            // 预置默认人格
            _personas.TryAdd(DefaultName, CreateDefaultPersona());
        }

        #region IPersonaService 实现

        public Models.Persona Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = DefaultName;

            if (!_personas.TryGetValue(name, out var persona))
            {
                // 自动降级到默认人格
                _personas.TryGetValue(DefaultName, out persona);
            }
            return persona;
        }

        public IReadOnlyList<Models.Persona> GetAll() => _personas.Values.ToList();

        public bool Add(Models.Persona persona)
        {
            if (persona == null) throw new ArgumentNullException(nameof(persona));
            return _personas.TryAdd(persona.Name, persona);
        }

        public bool Update(Models.Persona persona)
        {
            if (persona == null) throw new ArgumentNullException(nameof(persona));
            return _personas.AddOrUpdate(persona.Name, persona, (_, __) => persona) != null;
        }

        public bool Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, DefaultName, StringComparison.OrdinalIgnoreCase))
                return false; // 禁止删除默认人格
            return _personas.TryRemove(name, out _);
        }

        public Models.PersonaState GetStateForPersistence()
        {
            // Snapshot：转换为普通 Dictionary 以便序列化。
            var snapshot = _personas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            return new Models.PersonaState(snapshot);
        }

        public void LoadStateFromPersistence(Models.PersonaState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            _personas.Clear();
            foreach (var kvp in state.Personas)
            {
                _personas[kvp.Key] = kvp.Value;
            }

            // 数据完整性保障：若缺失默认人格则补齐
            if (!_personas.ContainsKey(DefaultName))
            {
                _personas.TryAdd(DefaultName, CreateDefaultPersona());
            }
        }

        #endregion
    }
}
