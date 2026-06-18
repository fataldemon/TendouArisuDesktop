# AliceBot 功能文档

> 2026-06-19 更新 — GPT-SoVITS TTS 集成 + 流式逐句播放 + 统一生命周期

---

## 一、系统架构概述

```
LLM → EmotionParser → EmotionPlayer → FacialEngine(表情, per-model profile)
                                     → BodyEngine(身体动画, Playable API 6层)
                                     → EyeTrackingController(眼动/头部跟踪, per-model)
                                     → BlinkController(眨眼抑制, per-model blinkConflictIndices)

TTS: BaiduTranslator(可选) → TtsCoordinator → GptSovits → PlayQueueLoop(统一生命周期)
                                               → BertVits2 / SimpleVitsApi(兼容旧版)

WPF ←→ PipeServer(TCP 19876) ←→ ActionSystemRuntime(数据管理)
                              ←→ ModelManager(VRM加载/恢复)
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
| 待机恢复 | `RestoreToIdle()` 统一触发 |

### 2.3 动作播放逻辑

1. 解析情绪 → 查 EmotionMapping → 得 ActionGroup + FacialOverride
2. 所有 6 个 bodyPart：有 clip → Play(0.35s 过渡)，无 clip 且非 fullBody → Stop(渐出)
3. Layer 0→1 平滑 FadeIn，1→0 平滑 FadeOut
4. 面部表情 CrossfadeTo(blendInFacial=0.15s, blendOutFacial=0.2s)
5. Blink 抑制 + 眼球跟踪由 `UpdateAuxiliary` 按动作组配置控制
6. loop clip: 播完后自动从头循环
7. 单播 clip: 播完自动回待机
8. loop clip + TTS: 句间不触发 `NotifyTTSEnd`，PlayQueueLoop 全程结束后统一 `RestoreToIdle`
9. loop clip 无 TTS: 持续 holdNoTTS → 恢复待机
10. 交互中 (触摸/拖拽): `suppressAutoEnd=true` 阻止自动结束

### 2.4 表情系统 v3 (Per-Model Expression Profile)

- 默认模型: 15 个全局硬编码预设, Inspector 值
- 导入 VRM: 自动扫描 `Expression.Clips` 映射 VRM 预设到 AliceBot 表情名
- 每个模型独立 profile JSON: `persistentDataPath/expressions/{modelKey}.json`
- WPF 表情预设编辑器: 可交互 BlendShape 选择 (ComboBox + 滑块 + 预览 + 保存)

### 2.5 眼部系统 (Per-Model Eye Profile)

- WPF "眼部动作" Tab: 眨眼/看左/看右/看上/看下 BlendShape 映射
- VRM 自动检测 `ExpressionPreset.blink/lookUp/lookDown/lookLeft/lookRight`
- 每模型独立 profile JSON: `persistentDataPath/eyes/{modelKey}.json`
- 眼球跟踪由动作组 `enableEyeTracking` 配置控制，不做额外覆盖

### 2.6 腮红

- `ApplyBlush(null)` → 禁用腮红渲染器
- `ApplyBlush("shy")` → shyBlushMaterial
- 其他 blushMode → normalBlushMaterial

---

## 三、TTS 语音系统

### 3.1 后端架构

| 模式 | 后端 | API |
|---|---|---|
| 0 (默认) | GPT-SoVITS | HTTP POST /tts，streaming_mode=true |
| 1 | Gradio/Bert-VITS2 | 三步：格式化→合成→加载 |
| 2 | Simple-Vits | multipart/form-data POST |
| 3 | None | 关闭 |

### 3.2 翻译

| 设置 | 说明 |
|------|------|
| 开启 | BaiduTranslate: zh→jp → TTS(all_ja) |
| 关闭 | 直接 TTS(zh) |

### 3.3 参考音频映射

- 每情绪两条：`ja`（日文，带默认 .wav）+ `zh`（中文，初始为空）
- `ja` 无文件时 fallback 到 `zh`，反之亦然
- WPF 面板支持日文/中文两个子 tab，可上传自定义 .wav，自动复制到 StreamingAssets/RefAudio/
- ▶ 按钮可直接在 WPF 播放试听参考音频

### 3.4 文本清洗（TTs前）

```
RemoveActionTag → RemoveEmotionTag → Regex去括号(（...）/ (...)) → 邦邦咔邦→パンパカパーン
```

### 3.5 分句策略 (SplitForTts)

- 按 `パンパカパーン` 先切大段（特殊 WAV 独立播放）
- 每段内按 `。！？!?\n` 切整句
- 累积 buffer 到 ≥15 字才切，短句自动和后面合并
- 最后尾巴兜底收尾

### 3.6 邦邦咔邦特殊处理

- 中文文本中"邦邦咔邦" → 替换为"パンパカパーン"（不经过翻译）
- SplitForTts 识别为独立 segment → 直接入队 bangbangkabang.wav → 跳过翻译和 TTS

---

## 四、统一生命周期 (PlayQueueLoop)

动作、对话框、音频三系统由 `PlayQueueLoop` 统一控制触发时机：

| 阶段 | 动作 | 对话框 | 音频 |
|---|---|---|---|
| `PROCESSING` (翻译+TTS合成) | — | — | 合成中 |
| `SPEAKING` 第一个 clip 出队 | PlayEmotion + NotifyTTSStart | onDialogue=true, dialogStartTime=now | PlayVoice |
| `SPEAKING` 持续 | 动作 loop | 显示（playQueue.Count>0或isPlaying时保持） | 播放/队列 |
| `SPEAKING` 退出 | RestoreToIdle | 10s floor → 关闭 | 停止 |
| 全部 TTS 失败 | `_ttsAllDispatched` 时触发 StartSpeak | 同上 | 无音频但动作和对话框依然弹出 |

---

## 五、WPF 设置面板

### 5.1 Tab 概览

| Tab | 功能 |
|-----|------|
| 连接设置 | WebSocket URL + 连接断开 |
| **语音设置** | TTS 引擎选择(GPTSovits/Gradio/SimpleVits/None) + API URL + 邦邦咔邦路径 |
| → 语音连接 | TTS 模式 + URL（三引擎独立）+ 保存/发送测试 |
| → 参考音频 | 日文/中文子 tab + 上传/播放/编辑 promptText |
| **翻译设置** | 翻译开关 + Baidu API 配置 |
| 对话框设置 | 宽高 + 最短保持时间 + 气泡/文字颜色 |
| 模型管理 | VRM 加载/恢复 + 历史（支持单条删除）+ 缩放 |
| 动画库 | 浏览/导入/预览 AnimationClip + 搜索过滤 |
| 动作系统 | 子 tab: 情绪映射 / 动作组 / 表情预设 / 眼部动作 |
| 对话记录 | 查看/清空对话历史 |

### 5.2 语音设置详情

- TTS 模式选择按钮：点即生效（`update_config`），三个引擎 URL 独立缓存
- 保存：URL 落盘到 `settings.json`
- 发送测试：先保存 → 再调 `test_tts`，状态 label 实时反馈成功/失败

### 5.3 模型管理

- 历史列表每行有"删"按钮，单击移除；持久化到 `model_history.json`
- 缩放滑块 + 应用按钮 (per-model 持久化到 `model_scales.json`)
- 恢复默认后模型选择持久化到 `settings.json`

---

## 六、窗口操作

| 操作 | 功能 |
|------|------|
| Ctrl+滚轮 | 缩放相机距离 (0.7-3.0m) |
| Ctrl+右键拖拽 | 绕角色旋转 |
| Shift+左键拖拽 | 平移镜头 |
| Ctrl+左键 | 拖拽窗口 + grip 对话框调整 |
| 触摸 | 鼠标靠近头部触发"触摸"动作 |

---

## 七、持久化文件总览

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
| 参考音频 | `StreamingAssets/RefAudio/` (含邦邦咔邦 WAV) |

---

## 八、数据流

```
启动时:
  ActionSystemRuntime.EnsureInit() → 硬编码默认 → JSON 覆盖
  ModelManager.Awake() → 存眼部快照
  GameStart.Start()
    → 加载 settings.json → ApplyFrom → 设默认 URL/BaseDir
    → 启动 PipeServer
    → 订阅 NetManager.OnConnectionChanged（WebSocket 断连自动通知 WPF）

WPF 编辑保存:
  WPF → TCP → PipeServer → 更新内存 → 写 JSON/SettingsData → RefreshInitData

运行时播放:
  LLM → NetManager → response_queue(容量50) → Update() 出队
    → ProcessResponse(去标签/括号/邦邦咔邦)
    → ProcessSentencesStreaming(分句→翻译→TTS→入队)
    → PlayQueueLoop:
        first clip → StartSpeak(PlayEmotion + NotifyTTSStart + dialog)
        播放队列按序播放，全部完成 → RestoreToIdle
        全部失败 → 仍然触发 StartSpeak（动作+对话框）
    对话框: playQueue.Count>0 || isPlaying → 保持
           Time.time - dialogStartTime >= 10s → 关闭（10s floor）
```
