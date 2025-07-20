# 🎯 RimAI 新手超简单指南

## 👋 写给完全新手

如果你：
- 只想要一个AI助手功能
- 不想学复杂的编程概念
- 希望复制粘贴就能工作

**这个指南就是为你准备的！**

---

## 🚀 30秒创建AI助手

### 第1步：复制这个文件

创建文件：`Source/Officers/SimpleAI.cs`

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Officers.Base;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 超简单的AI助手 - 新手专用
    /// </summary>
    public class SimpleAI : OfficerBase
    {
        // 这些是固定的，不要改
        private static SimpleAI _instance;
        public static SimpleAI Instance => _instance ??= new SimpleAI();
        private SimpleAI() { }
        
        // 这里你可以改成你喜欢的名字
        public override string Name => "我的AI助手";
        public override string Description => "一个超简单的AI助手";
        public override string IconPath => "UI/Icons/Governor";
        public override OfficerRole Role => OfficerRole.Governor;

        // 这里决定AI能看到什么信息
        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var context = new Dictionary<string, object>();
            
            var map = Find.CurrentMap;
            if (map != null)
            {
                // 告诉AI基本的游戏信息
                context["殖民者数量"] = map.mapPawns.FreeColonistsCount;
                context["天气情况"] = map.weatherManager.curWeather.label;
                context["当前季节"] = GenLocalDate.Season(map).ToString();
                
                // 你想让AI知道更多信息？在这里添加：
                // context["你的标签"] = "你的信息";
            }
            
            return context;
        }
    }
}
```

### 第2步：注册你的AI

打开文件：`Source/Architecture/ServiceContainer.cs`

找到 `RegisterDefaultServices()` 方法，在里面加一行：

```csharp
RegisterInstance<SimpleAI>(SimpleAI.Instance);
```

### 第3步：添加按钮

打开文件：`UI/MainTabWindow_RimAI.cs`

在 `DoWindowContents` 方法里加这段代码：

```csharp
if (Widgets.ButtonText(new Rect(10, 120, 200, 30), "我的AI助手"))
{
    var advice = await SimpleAI.Instance.GetAdviceAsync();
    Messages.Message(advice, MessageTypeDefOf.NeutralEvent);
}
```

### 第4步：完成！

重新编译，进游戏，点击"我的AI助手"按钮就行了！

---

## 🔧 简单定制

### 想改AI的名字？
修改这行：
```csharp
public override string Name => "你的新名字";
```

### 想让AI知道更多信息？
在 `BuildContextAsync` 方法里添加：
```csharp
context["食物存量"] = map.resourceCounter.TotalHumanEdibleNutrition;
context["威胁等级"] = StorytellerUtility.DefaultThreatPointsNow(map);
// 添加任何你想要的信息
```

### 想要多个AI助手？
复制整个 `SimpleAI.cs` 文件，改名为 `SimpleAI2.cs`，然后：
1. 把类名从 `SimpleAI` 改成 `SimpleAI2`
2. 改变 `Name` 和 `Description`
3. 记得注册新的服务

---

## ❓ 遇到问题？

**编译失败？**
- 检查是否忘记添加分号 `;`
- 检查大括号 `{}` 是否匹配

**按钮没反应？**
- 检查是否注册了服务
- 检查UI代码是否正确添加

**AI回答很奇怪？**
- 检查 `BuildContextAsync` 中的信息是否正确
- 确保 RimAI Framework 正常工作

**还是不行？**
查看游戏的调试日志，通常会有错误提示。

---

## 🎉 成功后你能做什么？

一旦你的简单AI工作了，你可以：

1. **让AI更聪明** - 添加更多游戏信息
2. **创建专门的AI** - 比如专门管理食物的AI、管理建设的AI
3. **学习高级功能** - 查看完整的组件创建指南

**记住**：从简单开始，慢慢学习！🚀

---

## 📚 示例模板库

### 食物管理AI
```csharp
context["食物储量"] = map.resourceCounter.TotalHumanEdibleNutrition;
context["饥饿的殖民者"] = map.mapPawns.FreeColonists.Count(p => p.needs.food.CurLevelPercentage < 0.3f);
```

### 建设管理AI  
```csharp
context["未完成建设"] = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count();
context["建筑材料_钢材"] = map.resourceCounter.GetCount(ThingDefOf.Steel);
context["建筑材料_木材"] = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
```

### 医疗管理AI
```csharp
context["受伤殖民者"] = map.mapPawns.FreeColonists.Count(p => p.health.HasHediffsNeedingTend());
context["生病殖民者"] = map.mapPawns.FreeColonists.Count(p => p.health.State != PawnHealthState.Mobile);
```

复制这些模板，替换掉基础的 `BuildContextAsync` 方法内容即可！
