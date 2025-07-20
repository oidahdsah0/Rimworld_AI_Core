# CoreSettings窗口优化报告

## 🎯 优化目标

根据用户建议，检查并优化 CoreSettingsWindow.cs 中的设置内容，消除与 Dialog_OfficerSettings.cs 的重复设置，避免配置混乱。

## 🔍 问题分析

### 发现的重复内容：

1. **官员设置重复**：
   - `CoreSettingsWindow.cs` 中有 `DrawOfficerSettings()` 方法
   - `Dialog_OfficerSettings.cs` 已经提供了完整的官员配置界面
   - 造成设置分散，用户体验混乱

2. **基础设置重复**：
   - UI选项、缓存设置在两个窗口中都存在
   - 性能设置有部分重复
   - 用户不知道应该在哪里配置

3. **标签页冗余**：
   - CoreSettings 有5个标签页，其中2个与官员设置窗口重复

## 🔧 优化方案

### 明确窗口职责分工：

1. **CoreSettingsWindow.cs** - 核心系统设置
   - 系统级配置（事件总线、核心服务）
   - 高级系统设置（调试、性能监控）
   - 系统维护操作

2. **Dialog_OfficerSettings.cs** - 用户功能设置
   - AI官员配置
   - 基础UI设置
   - 日常使用的性能设置

## ✅ 具体优化内容

### 1. CoreSettingsWindow.cs 重构

#### 标签页精简：
- **之前**：5个标签页（常规、官员、性能、高级、调试）
- **之后**：4个标签页（系统、性能、高级、调试）
- **删除**：官员标签页（已迁移到 Dialog_OfficerSettings.cs）

#### 功能重新分工：

**🖥️ 系统设置标签页**（原常规标签页）：
```csharp
- 事件监控系统配置
- 核心框架状态显示
- 系统服务重新初始化
```

**⚡ 性能设置标签页**（专注高级性能）：
```csharp
- 高级性能监控
- 实时性能统计
- 系统资源监控
- 性能基准测试
```

**🔬 高级设置标签页**（增强系统级设置）：
```csharp
- 调试选项（详细日志、性能分析）
- UI高级配置
- 系统维护操作
- 设置导入/导出
```

**🐛 调试信息标签页**（保持不变）：
```csharp
- 系统调试信息
- 事件系统测试
- 缓存管理
```

### 2. 新增设置类

#### DebugSettings 类：
```csharp
public class DebugSettings : IExposable
{
    public bool EnableVerboseLogging = false;
    public bool EnablePerformanceProfiling = false;
    public bool SaveAnalysisResults = false;
    public bool ShowInternalEvents = false;
}
```

#### PerformanceSettings 扩展：
```csharp
// 新增属性
public bool EnableMemoryMonitoring = false;
```

### 3. 删除的重复内容

#### 移除的方法：
- `DrawOfficerSettings()`
- `DrawOfficerConfig()`

#### 移除的UI元素：
- 官员启用/禁用复选框
- 官员参数配置滑块
- 基础缓存设置（保留在官员设置窗口）
- 基础UI设置（保留在官员设置窗口）

## 🎨 用户体验改进

### 清晰的设置分工：

1. **日常使用** → 使用主界面的"官员设置"按钮
   - AI官员开关
   - 基础参数调节
   - 常用性能设置

2. **系统配置** → 使用Mod设置中的Core设置
   - 核心服务配置
   - 高级调试选项
   - 系统维护操作

### 避免混乱：

- **提示信息**：在系统设置中添加提示，引导用户到正确的设置位置
- **功能专一**：每个设置项只在一个地方出现
- **逻辑清晰**：系统级vs用户级设置明确分离

## 📊 优化效果

### 代码质量：
- ✅ 消除重复代码 ~150行
- ✅ 提高代码可维护性
- ✅ 清晰的职责分离

### 用户体验：
- ✅ 设置项不再重复
- ✅ 配置逻辑更清晰
- ✅ 找到设置更容易

### 系统稳定性：
- ✅ 减少配置冲突可能
- ✅ 简化错误处理逻辑
- ✅ 更好的模块化设计

## 🔍 技术实现细节

### 枚举更新：
```csharp
// 之前
public enum SettingsTab
{
    General, Officers, Performance, Advanced, Debug
}

// 之后  
public enum SettingsTab
{
    General,    // 重命名为系统设置
    Performance,
    Advanced,
    Debug
}
```

### 标签页布局调整：
```csharp
// 从5个标签页改为4个标签页
var tabWidth = rect.width / 4; // 之前是 /5
var tabNames = new[] { "系统", "性能", "高级", "调试" };
```

### 设置序列化增强：
```csharp
// 新增调试设置序列化
Scribe_Deep.Look(ref Debug, "debug");
if (Debug == null) Debug = new DebugSettings();
```

## 🚀 后续建议

1. **用户引导**：考虑在游戏中添加设置位置的提示说明
2. **设置同步**：考虑在两个设置窗口之间添加快捷跳转
3. **配置验证**：添加设置一致性检查机制
4. **文档更新**：更新用户手册中的设置说明

---

**优化完成时间**：2025年7月20日  
**影响文件**：
- `CoreSettingsWindow.cs` - 大幅重构
- `CoreSettings.cs` - 新增DebugSettings类和属性
**代码质量**：✅ 无编译错误，通过验证
**用户体验**：✅ 设置逻辑更清晰，避免重复配置
