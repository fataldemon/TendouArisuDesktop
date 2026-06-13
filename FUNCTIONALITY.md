# AliceBot 功能文档

> 2026-06-13 动作系统 v2 重构后

---

## 一、系统架构概述

```
LLM → EmotionParser → EmotionPlayer → FacialEngine(表情)
                                     → BodyEngine(身体动画, Playable API 6层)
                                     → EyeTrackingController(眼动/头部跟踪)
                                     → BlinkController(眨眼抑制)
WPF ←→ PipeServer(TCP 19876) ←→ ActionSystemRuntime(数据管理)
```

---

## 二、动作系统 (Action System)

### 2.1 核心概念

| 概念 | 说明 |
|------|------|
| **情绪映射** | Chinese 情绪词 → 动作组 + 表情覆盖/权重 |
| **动作组** | 一个完整的身体动作配置：每个身体部位对应一个 AnimationClip + 循环/单播 + 过渡时长 |
| **表情预设** | 15 个 BlendShape 配方 + 特效对象(Tear/Sweat/Blush) |
| **身体部位** | fullBody, upperBody, head, leftArm, rightArm, lowerBody (6 层独立播放) |

### 2.2 触发方式

| 触发源 | 说明 |
|--------|------|
| LLM 回复 | 回复文本中 `【{'expression':'开心'}】` 格式的情绪标签 |
| 触摸 | 鼠标左键点击角色头部区域 → 播放"触摸"映射的动作组 |
| 拖拽 | Ctrl+拖拽对话框 → 播放"拖拽"映射的动作组 |
| 随机事件 | 空闲 30~50 秒随机触发 `isRandomEvent=true` 的情绪映射中随机一个 |
| 待机恢复 | 动作结束 / TTS 结束 → 播放"待机"映射的动作组 |

### 2.3 动作播放逻辑

1. 解析情绪 → 查 `EmotionMapping` → 得 ActionGroup + FacialOverride
2. 对所有 6 个 bodyPart：有 clip → Play(0.35s 过渡)，无 clip 且非 fullBody → Stop(渐出)
3. Layer(推子) 0→1 平滑 FadeIn，1→0 平滑 FadeOut
4. 面部表情 CrossfadeTo(blendInFacial=0.15s, blendOutFacial=0.2s)
5. Blink 抑制 + 眼球跟踪 (若 enableEyeTracking=true 则不抑制)
6. 循环 clip: 播完后自动从头循环
7. 单播 clip: 播完自动回待机
8. 循环 clip + TTS: TTS 结束→等待 holdAfterTTS(默认3s) → 恢复待机
9. 循环 clip 无 TTS: 持续 holdNoTTS(默认4s) → 恢复待机

### 2.4 表情覆盖 (FacialOverride)

- 情绪映射中的 `表情覆盖` 优先于动作组的 `默认表情`
- 覆盖为空 → 使用动作组默认
- 权重 0~1 (WPF 滑块)

### 2.5 眼球/头部跟踪

- Idle 组默认 `enableEyeTracking=true` (跟踪开启)
- 其他动作组默认 `false` (动作时跟踪关闭)
- 可在 WPF 动作组编辑器每个组单独设置
- 仅由 `expressionActive` 控制，不再被 `IsAnyNonIdlePlaying()` 独立关闭

### 2.6 Apply Root Motion

- 每个动作组独立设置 `allowRootMotion`
- 默认 `false` (不位移)
- 播放时自动同步到 `animator.applyRootMotion`
- 预览时：全局 checkbox(动画库 tab) 控制预览模式的 ARM
- 正常播放时：由动作组配置决定

---

## 三、WPF 设置面板

### 3.1 Tab 概览

| Tab | 功能 |
|-----|------|
| 连接设置 | WebSocket URL + TTS 配置 + 翻译配置 |
| 对话框设置 | 对话框宽度/高度 + 最短保持时间(秒) |
| 模型管理 | VRM 模型加载/恢复默认 |
| 动画库 | 浏览/导入/预览 AnimationClip + 搜索过滤 |
| 动作系统 | 子 tab: 情绪映射 / 动作组 / 表情预设 |
| 对话记录 | 查看/清空对话历史 |

### 3.2 情绪映射编辑器

- **左侧列表**: 72 条映射，★=系统保留, 🎲=随机事件
- **编辑面板**: 情绪名称 + 动作组下拉 + 表情覆盖按钮 + 权重 slider + 随机事件 checkbox
- **预览**: 点"预览" → 全局预览(表情+所有部位 clip+ARM+ET)
- **预览表情**: 仅表情 + 镜头缩放
- **双击列表项** → 直接编辑

### 3.3 动作组编辑器

- **左侧列表**: ★ Idle + 用户自定义组，可删除(Idle 不可删)
- **编辑面板**: 组名(TextBox 可编辑) + ARM checkbox + ET checkbox +
  每个部位: [搜索框] [下拉选 clip] [▶预览该部位] + 表情选择器 + 表情权重 + 表情预览
- **全局预览**: 一键播放所有部位 clip + 表情 + ARM + ET
- **新建按钮**: 创建空白动作组
- **保存**: 保存数据 + 退出预览恢复 idle + 立即生效(若当前正在播放该组)
- **双击列表项** → 直接编辑

### 3.4 表情预设编辑器

- **左侧列表**: 15 个表情预设名
- **编辑面板**: BlendShape 目标 index/weight 查看 + 特效列表 + 腮红模式
- **预览**: 带镜头缩放到脸
- **双击列表项** → 直接编辑

---

## 四、预览系统

### 4.1 进入/退出

| 预览类型 | 进入方式 | 退出方式 | 镜头 |
|---------|---------|---------|:--:|
| 动画库 ▶ | 点列表项 ▶ 按钮 | 切换 tab / 点停止 | 不缩放 |
| 动作组小三角 ▶ | 每个部位旁 ▶ | 切换 tab / 点停止 | 不缩放 |
| 动作组全局预览 | 点"全局预览" | 切换 tab / 点停止 | 不缩放 |
| 表情预设预览 | 点"预览表情" | 切换 tab / 点停止 | 缩放到脸 |
| 情绪映射预览 | 点"预览" | 切换 tab / 点停止 | 不缩放 |
| 情绪映射预览表情 | 点"预览表情" | 切换 tab / 点停止 | 缩放到脸 |

### 4.2 预览机制

- 全局预览: 构建临时 `ActionGroupConfig`，走正常 `PlayableGraph` 通道 (非 SampleAnimation)
- 位置管理: 进入时锁角色位置，退出时恢复，切换预览先归位再锁新
- ARM: 预览时跟随 `animator.applyRootMotion`，勾选后有位移，不勾选则锁定原地
- ET: 预览时跟随 `config.enableEyeTracking` 和 `expressionActive`

---

## 五、WPF 配置持久化

| 保存时 | 文件 |
|--------|------|
| 情绪映射修改 | `persistentDataPath/emotion_mappings_v2.json` |
| 动作组修改 | `persistentDataPath/action_groups_v2.json` |
| 表情预设修改 | `persistentDataPath/facial_presets_v2.json` |
| 所有设置 | `persistentDataPath/settings.json` |

启动时：先加载硬编码默认值 → 检查 JSON 文件 → 合并覆盖 → 运行时使用合并后的数据。

---

## 六、对话框

- LLM 回复出现 → 气泡显示
- 滚动动画: 文字自动滚动，到底暂停 → 回顶部再滚
- 保持时间: `max(用户设置, 循环?10s : max(10s, clip时长+3s))`
- 音频播放中: 不计时（保持显示）
- 无音频: 直接开始倒计时
- Ctrl+滚轮: 调整字号
- Ctrl+右拖: 绕角色旋转相机
- Ctrl+滚轮(靠近头部): 缩放相机距离
- Ctrl+拖对话框 grip: 调整对话框位置(持久化)
- 默认最短保持 10s，WPF 可设

---

## 七、托盘菜单

- 右键托盘图标 → 弹出菜单
- 设置 / 模型管理 / 动画库 / 动作系统 / 对话记录 → 打开对应 WPF Tab
- 左键托盘图标 → 切换窗口显示/隐藏
- 退出 → 关闭 Unity + WPF

---

## 八、数据流

```
启动时:
  ActionSystemRuntime.EnsureInit()
    → 硬编码默认(ActionSystemDefaults)
    → JSON 覆盖(ActionSystemJsonIO)
    → 合并 → 可用

WPF 编辑保存:
  WPF → TCP → PipeServer → ActionSystemRuntime(更新内存) → 写 JSON → RefreshInit 发给 WPF

运行时播放:
  LLM → EmotionParser → PlayEmotion → ActionSystemRuntime 查数据
    → FacialEngine(BlendShape) + BodyEngine(PlayableGraph)
```
