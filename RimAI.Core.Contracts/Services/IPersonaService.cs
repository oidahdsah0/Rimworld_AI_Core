using System.Collections.Generic;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 人格管理服务接口。
    /// 提供对 Persona 的完整 CRUD 能力，并允许导入/导出状态以实现持久化。
    /// </summary>
    public interface IPersonaService
    {
        /// <summary>
        /// 根据名称获取人格；不存在时返回 null。
        /// </summary>
        Persona Get(string name);

        /// <summary>
        /// 获取全部人格列表（只读）。
        /// </summary>
        IReadOnlyList<Persona> GetAll();

        /// <summary>
        /// 新增人格；名称冲突时返回 false。
        /// </summary>
        bool Add(Persona persona);

        /// <summary>
        /// 更新已有的人格；若不存在返回 false。
        /// </summary>
        bool Update(Persona persona);

        /// <summary>
        /// 删除指定名称的人格；若不存在返回 false。
        /// </summary>
        bool Delete(string name);

        /// <summary>
        /// 导出内部状态，供持久化层序列化。
        /// </summary>
        PersonaState GetStateForPersistence();

        /// <summary>
        /// 从持久化状态恢复内部数据结构。
        /// </summary>
        void LoadStateFromPersistence(PersonaState state);
    }
}
