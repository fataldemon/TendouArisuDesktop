# AliceBot 功能文档

> 2026-06-14 更新 — 表情系统 v3 + 眼部系统 + 窗口操作重构

---

## 一、系统架构概述

```
LLM → EmotionParser → EmotionPlayer → FacialEngine(表情, per-model profile)
                                     → BodyEngine(身体动画, Playable API 6层)
                                     → EyeTrackingController(眼动/头部跟踪, per-model)
                                     → BlinkController(眨眼抑制, per-model blinkConflictIndices)
WPF ←→ PipeServer(TCP 19876) ←→ ActionSystemRuntime(数据管理)
                              ←→ ModelManager(VRM加载/恢复)
                              ←→ AttachmentManager(挂载系统, 待实现)
```

---

## 二、动作系统 (Action System)

### 2.1 核心概念

| 概念 | 说明 |
|------|------|
| **情绪映射** | Chinese 情绪词 → 动作组 + 表情覆盖/权重 |
| **动作组** | 完整的身体动作配置：每个身体部位对应一个 AnimationClip + loop + blend |
| **表情预设** | BlendShape 配方 + 特效对象(Tear/Sweat/Blush); per-model 独立配置 |
| **身体部位** | fullBody, upperBody, head, leftArm, rightArm, lowerBody (6 层独立播放) |

### 2.2 触发方式

| 触发源 | 说明 |
|--------|------|
| LLM 回复 | 回复文本中 `【{'expression':'开心'}】` 格式的情绪标签 |
| 触摸 | 鼠标左键靠近角色头部 (2D 屏幕距离) → "触摸" 动作组 |
| 拖拽 | Ctrl+左键点中角色附近 → "拖拽" 动作组; 拖拽中 loop 动画不自动停 (suppressAutoEnd) |
| 随机事件 | 空闲 30~50 秒随机触发 isRandomEvent 的情绪映射 |
| 待机恢复 | 动作结束 / TTS 结束 → "待机" 动作组 |

### 2.3 动作播放逻辑

1. 解析情绪 → 查 EmotionMapping → 得 ActionGroup + FacialOverride
2. 所有 6 个 bodyPart：有 clip → Play(0.35s 过渡)，无 clip 且非 fullBody → Stop(渐出)
3. Layer 0→1 平滑 FadeIn，1→0 平滑 FadeOut
4. 面部表情 CrossfadeTo(blendInFacial=0.15s, blendOutFacial=0.2s)
5. Blink 抑制 + 眼球跟踪
6. loop clip: 播完后自动从头循环
7. 单播 clip: 播完自动回待机
8. loop clip + TTS: TTS 结束→等待 holdAfterTTS → 恢复待机
9. loop clip 无 TTS: 持续 holdNoTTS → 恢复待机
10. 交互中 (触摸/拖拽): `suppressAutoEnd=true` 阻止自动结束; 释放后 loop=true 自然过渡 → loop=false 立即回待机

### 2.4 表情系统 v3 (Per-Model Expression Profile)

- 默认模型: 15 个全局硬编码预设, Inspector 值
- 导入 VRM: 自动扫描 `Expression.Clips` 映射 VRM 预设到 AliceBot 表情名
- 每个模型独立 profile JSON: `persistentDataPath/expressions/{modelKey}.json`
- WPF 表情预设编辑器: 可交互 BlendShape 选择 (ComboBox + 滑块 + 预览 + 保存)
- 预览实时生效: 编辑中的 targets 直写 mesh, 含特效/腮红
- `FacialEngine.GetPreset`: 先查模型 profile → 回退全局硬编码

### 2.5 眼部系统 (Per-Model Eye Profile)

- WPF "眼部动作" Tab (动作系统子 Tab): 眨眼/看左/看右/看上/看下 BlendShape 映射
- VRM 自动检测 `ExpressionPreset.blink/lookUp/lookDown/lookLeft/lookRight`
- 每模型独立 profile JSON: `persistentDataPath/eyes/{modelKey}.json`
- 眼球追踪参数: 眼球移动 (10-300) + 头部转动 (0-30) 滑块
- 每行 `👁` 预览: 镜头缩脸 + 暂停跟踪 → 看清效果
- 默认模型: 使用 Inspector 硬编码值; 导入 VRM 自动覆盖

### 2.6 腮红

- `ApplyBlush(null)` → 禁用腮红渲染器
- `ApplyBlush("shy")` → shyBlushMaterial
- 其他 blushMode → normalBlushMaterial
- 特效对象 (Tear/Sweat) 由表情预设的 `activateObjects` 控制

---

## 三、WPF 设置面板

### 3.1 Tab 概览

| Tab | 功能 |
|-----|------|
| 连接设置 | WebSocket URL + TTS 配置 + 翻译配置 |
| 对话框设置 | 宽高 + 最短保持时间 + **气泡颜色 (取色盘)** + **文字颜色** |
| 模型管理 | VRM 加载/恢复 + 加载历史 + **每模型缩放 (0.1-3.0)** + **模型选择持久化** |
| 动画库 | 浏览/导入/预览 AnimationClip + 搜索过滤 |
| 动作系统 | 子 tab: 情绪映射 / 动作组 / 表情预设 / **眼部动作** |
| 对话记录 | 查看/清空对话历史 |

### 3.2 模型管理

- 历史列表双击加载; 选中同步路径到文本框
- 缩放滑块 + 应用按钮 (per-model 持久化到 `model_scales.json`)
- 恢复默认后模型选择持久化到 `settings.json`, 启动自动加载

### 3.3 表情预设编辑器

- 左侧列表: 15 个表情预设名 (导入 VRM 后根据引用动态生成)
- 编辑面板: ComboBox 选 BlendShape + 权重滑块 + 删除按钮 + 添加 BlendShape
- **预览**: 实时生效 (targets + 特效 + 腮红), 镜头缩放到脸
- **保存**: 写入 per-model profile JSON
- `selectEyeIndex` 边界安全

### 3.4 眼部动作编辑器

- 5 个眼部 BlendShape 映射: 眨眼/看左/看右/看上/看下
- 每行 ComboBox (BlendShape 名) + `👁` 预览按钮 (仅该方向)
- 眼球移动 + 头部转动 Slider
- 应用: 保存 per-model profile; 恢复自动检测: 从 VRM 重建

### 3.5 对话框设置

- 气泡颜色: 取色盘按钮 → 选颜色 → 色块预览
- 文字颜色: 同上
- 保存 → `update_bubble_color` → 即时重绘 Texture2D

---

## 四、窗口操作 (重构)

### 4.1 拖拽

- **Ctrl+左键**: 启动窗口拖拽; 用 `GetCursorPos + SetWindowPos` 非阻塞替代 WM_NCLBUTTONDOWN
- **命中检测**: 鼠标离模型屏幕坐标 400px 内才启动拖拽
- **grip 对话框调整**: Input 驱动, `GetCursorPos` 坐标转换, 和窗口拖拽互斥
- **currentX/Y**: 初始化从 `ApplyWindowStyleDelayed` 的 `SetWindowPos` 同步

### 4.2 镜头控制

| 操作 | 功能 |
|------|------|
| Ctrl+滚轮 | 缩放相机距离 (0.7-3.0m) |
| Ctrl+右键拖拽 | 绕角色旋转 |
| Shift+左键拖拽 | **平移镜头** (panSpeed=0.006, Space.Self) |

### 4.3 输入机制

- `GetAsyncKeyState` 替代 `Input.GetMouseButton` (透明窗口兼容)
- 触摸/拖拽互斥: `!ctrlDown && !shiftDown` 条件
- `OnDragEnd` 设 `_isTouching=true` 防止拖拽松手误触发触摸

---

## 五、模型管理

| 功能 | 说明 |
|------|------|
| VRM 加载 | `Vrm10.LoadPathAsync`, `ReplaceModel` 自动关联 Animator / BodyEngine / FacialEngine |
| `FindBestBlendShapeRenderer` | 找 BlendShape 最多的 SkinnedMeshRenderer (脸部) |
| Per-model 表情 | VRM Expression.Clips 自动映射 → `ModelExpressionProfile` JSON |
| Per-model 眼部 | VRM 眼部 ExpressionPreset 自动映射 → `ModelEyeProfile` JSON |
| Per-model 缩放 | `model_scales.json`: modelKey → scale (0.1-3.0) |
| 模型选择持久化 | `settings.json` → `currentModelPath`, 启动自动加载 |
| 恢复默认 | `_defaultEyeProfile` 快照 → 恢复到 Inspector 初始值 |

---

## 六、持久化文件总览

| 数据 | 路径 |
|------|------|
| 全部设置 | `persistentDataPath/settings.json` |
| 情绪映射 | `persistentDataPath/emotion_mappings_v2.json` |
| 动作组 | `persistentDataPath/action_groups_v2.json` |
| 表情预设 (全局) | `persistentDataPath/facial_presets_v2.json` |
| Per-model 表情 | `persistentDataPath/expressions/{modelKey}.json` |
| Per-model 眼部 | `persistentDataPath/eyes/{modelKey}.json` |
| Per-model 缩放 | `persistentDataPath/model_scales.json` |
| 动画库 | `persistentDataPath/animation_library.json` |
| 模型历史 | `persistentDataPath/model_history.json` |

---

## 七、数据流

```
启动时:
  ActionSystemRuntime.EnsureInit()
    → 硬编码默认 → JSON 覆盖 → 合并
  ModelManager.Awake() → 存眼部快照
  GameStart.Start()
    → 加载 settings.json → 自动加载 currentModelPath
    → 加载 per-model 表情/眼部/缩放 profiles

WPF 编辑保存:
  WPF → TCP → PipeServer → (更新内存) → 写 JSON → RefreshInitData

运行时播放:
  LLM → EmotionParser → PlayEmotion → ActionSystemRuntime 查数据
    → FacialEngine(BlendShape, per-model profile)
    → BodyEngine(PlayableGraph)
    → EyeTrackingController(per-model eye indices)
```

---

## 八、待实现 (V2)

| 功能 | 状态 |
|------|:--:|
| 挂载系统 (物品库/持続挂载/动作挂载) | 设计完成, `feature/attachment-system` 分支 |
| Apply Root Motion 正常播放 | 待实现 |
| 多部位动画全局预览带 Mask | 待实现 |
| BlinkController per-model 眼部 BlendShape 自动收集 | 已实现 |
