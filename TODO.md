# TODO

> 2026-06-19 — GPT-SoVITS + 流式播放入库，逐个修小bug

## 已完成功能 (本 session: 06-17 ~ 06-19)

| 功能 | 状态 |
|------|:--:|
| GPT-SoVITS TTS 后端 (HTTP POST /tts) | ✅ |
| 流式逐句播放 (分句≥15字 → 翻译+TTS → PlayQueueLoop) | ✅ |
| 邦邦咔邦特殊处理 (替换パンパカパーン → 独立播放 bang.wav) | ✅ |
| 中日文双语言参考音频 (ja/zh 子 tab + fallback) | ✅ |
| 参考音频上传自动复制 + WPF ▶ 播放试听 | ✅ |
| 统一生命周期 (PlayQueueLoop: 动作/对话框/音频同步触发) | ✅ |
| 翻译开关 (翻译设置 tab + toggle) | ✅ |
| 三个 TTS 引擎 URL 独立管理 + 持久化 | ✅ |
| WebSocket 断连自动通知 WPF (OnConnectionChanged) | ✅ |
| 模型历史单条删除 | ✅ |
| TTS 文本清洗 (去标签/括号/邦邦咔邦) | ✅ |
| GPT-SoVITS 组件 m_PostURL 序列化 (Inspector 设默认) | ✅ |
| SaveRefAudioConfig 增量更新 (不全量覆盖) | ✅ |
| 迁移逻辑 (仅旧 Gradio 用户触发) | ✅ |
| 对话框绝对时间 + 10s floor | ✅ |
| 句间不 NotifyTTSEnd (动作不断) | ✅ |
| LLM chunk / 翻译 / TTS 全链路日志 | ✅ |
| 眼球跟踪由动作组 enableEyeTracking 配置控制 | ✅ |
| response_queue 容量 5→50 | ✅ |

## 临时方案待优化

| # | 问题 | 建议 |
|---|------|------|
| 1 | `response_queue` 容量上限 50 | 改为环形同步队列或基于时间戳的丢弃策略 |
| 2 | `SplitForTts` 仅按长度切分，不按语义 | 未来可接 LLM 的分句标记 |
| 3 | 中文参考音频默认空，需手动上传 | 当前 fallback 到日文 |
| 4 | 参考音频 promptText 日文硬编码 | 后续由用户自定义 |

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| 1 | 正常播放也支持 apply root motion | 中 |
| 2 | 多部位动画全局预览带 Mask | 中 |
| 3 | 随机事件 loop/时长优化 | 小 |
| 4 | 消息多时卡死修复 (OnGUI/RoundedBg/Regex 优化) | 中 |
| 5 | 挂载系统 (附件 + 物品库 + 持続/动作挂载) | 设计完成 |

## 待实现功能 — Mate-Engine 动画借鉴路线图

> 参考 `F:\GitRepository\Mate-Engine`。纯手写无第三方依赖(与现有架构一致),适配 URP + 现有 6 层 Playable 架构。

### ✅ 阶段 5:动画片段搬运(已完成)

| 内容 | 状态 | 提交 |
|------|:--:|------|
| 导入 117 个 Mate-Engine Humanoid 身体片段到 `Assets/ImportedAnimations/MateEngine/`(13 类) | ✅ | `312d8bb` |

### 🔴 阶段 1:程序化生命感(最高收益 — Mate "顺滑感"的灵魂)

| # | 任务 | 借鉴自 Mate-Engine | 复杂度 |
|---|------|---------------------|:--:|
| 1 | `SpringSolver.cs` 临界阻尼弹簧工具 + 帧率无关平滑(`1-Mathf.Exp(-k*dt)`) | `AvatarSwayController` | 小 |
| 2 | `AdditiveBoneLayer.cs` 逆四元数叠加骨骼层(与 6 层 Playable 共存) | `AvatarSwayController` | 中 |
| 3 | `IdleBreathingController.cs` 噪声驱动呼吸(脊椎/胸骨微缩放),接 EmotionPlayer idle | 原创 | 小 |
| 4 | `DragInertiaController.cs` 拖拽惯性前倾 | `AvatarSwayController` | 中 |
| 5 | `MouseLookAtDriver.cs` 升级 EyeTrackingController(driver-bone delta + 脊柱级联权重) | `AvatarMouseTracking` | 中 |
| 6 | 修复 `BodyEngine`/`FacialEngine` 帧率相关 `Lerp(a,b,k*dt)` | — | 小 |

### 🟡 阶段 2:UI 动效(范围小,就一个对话框)

| # | 任务 | 借鉴自 | 复杂度 |
|---|------|--------|:--:|
| 7 | `BubbleAnimator.cs` 淡入/缩放/打字机(替代 `GameStart.cs:796` 的 IMGUI) | 原创 | 中 |
| 8 | `UIBlur.shader` URP 重写 Poisson 盘模糊毛玻璃(可选) | `UiBlur.shader` | 中 |

### 🟡 阶段 3:情绪反馈粒子

| # | 任务 | 借鉴自 | 复杂度 |
|---|------|--------|:--:|
| 9 | `EmotionVFXController.cs` 数据驱动爱心/汗滴/震屏(Unity ParticleSystem) | 原创 | 中 |
| 10 | 替换静态 Drops 网格为带动画粒子 | — | 小 |

### 🟡 阶段 4:嘴形同步与音频

| # | 任务 | 借鉴自 | 复杂度 |
|---|------|--------|:--:|
| 11 | 升级 `AudioMouthController` 为音素驱动,接入 TTS 生命周期 | 原创 | 中 |
| 12 | 音频驱动 idle 微律动(可选) | `AvatarAnimatorController`(NAudio) | 中 |
