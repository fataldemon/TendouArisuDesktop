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
