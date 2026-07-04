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

### ✅ 阶段 1:程序化生命感(已完成 `04e17a3`)

| 内容 | 状态 |
|------|:--:|
| SpringMath.cs(弹簧 + 帧率无关平滑) | ✅ |
| IdleBreathingController.cs(胸腔 pitch 微旋转,挂 MagicaManager.OnPreSimulation) | ✅ |
| DragInertiaController.cs(拖拽惯性根 Transform 倾斜) | ✅ |
| EyeTrackingController.cs(5 处 Lerp→Damp 帧率修复) | ✅ |

### ✅ 阶段 2:UI 动效(已完成 `a16d537`)

| 内容 | 状态 |
|------|:--:|
| BubbleAnimator.cs(淡入/缩放弹出/滑入/打字机) | ✅ |
| GameStart.OnGUI(GUI.color/GUI.matrix/Substring 集成) | ✅ |
| 打字机完成后自动滚动长文本 | ✅ |

### ✅ 阶段 5:动画片段搬运(已完成 `312d8bb`)

| 内容 | 状态 |
|------|:--:|
| 导入 117 个 Mate-Engine Humanoid 身体片段(13 类) | ✅ |

### ✅ 阶段 6:窗口吸附 v1(已完成 `449bd8f`)

| 内容 | 状态 |
|------|:--:|
| WindowSnapController(EnumWindows 吸附/跟随/座位校准) | ✅ |
| 可见窗口过滤(IsSitEligibleWindow + Z 序遮挡检测) | ✅ |
| 身体锁(BodyLockActionGroup,消息时不起身) | ✅ |
| 平滑滑入 + 目标移动时紧跟不脱节 | ✅ |
| 情绪映射合并修复(新默认情绪对旧配置可见) | ✅ |

### ❌ 头发/背部遮挡(暂缓 — URP 透明叠加限制)

尝试了多种方案,均在 build 中完全无效:

| 方案 | 结果 |
|------|------|
| fragment discard/clip(屏幕 Y / 深度 Z / Y+Z 组合 / SV_POSITION 修正) | discard 在此环境不生效 |
| 外部深度遮挡体(Queue Geometry-1 + DepthOnly pass) | URP 深度管线不配合 |
| z-order avatar 插到目标窗口层级 | SetWindowPos 语义搞反 |

**结论**:URP 透明叠加窗口(WS_EX_LAYERED + DWM)下,fragment discard 和深度测试均不可靠。
**未来方向**:URP 后处理 RendererFeature(全屏 pass,渲染完后改 alpha)——完全不同的机制,绕开 fragment shader 限制。

### ⏳ 阶段 3:情绪反馈粒子

| # | 任务 | 借鉴自 | 复杂度 |
|---|------|--------|:--:|
| 9 | `EmotionVFXController.cs` 数据驱动爱心/汗滴/震屏(Unity ParticleSystem) | 原创 | 中 |
| 10 | 替换静态 Drops 网格为带动画粒子 | — | 小 |

### ⏳ 阶段 4:嘴形同步与音频

| # | 任务 | 借鉴自 | 复杂度 |
|---|------|--------|:--:|
| 11 | 升级 `AudioMouthController` 为音素驱动,接入 TTS 生命周期 | 原创 | 中 |
| 12 | 音频驱动 idle 微律动(可选) | `AvatarAnimatorController`(NAudio) | 中 |
