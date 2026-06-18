# AliceBot Bug 清单

> 2026-06-19 — GPT-SoVITS session 修复累积 bug

---

## 已修复 (本 session: 06-17 ~ 06-19)

| # | 问题 | 修复方式 |
|---|------|---------|
| 1 | URL 持久化被 `SaveRefAudioConfig` 全量覆盖湮灭 | 增量更新，从 config 补齐 gptSovitsUrl 等字段 |
| 2 | 迁移逻辑误杀新用户（空 gptSovitsUrl → tts=1） | 加 `!string.IsNullOrEmpty(settings.gradioUrl)` 条件 |
| 3 | GPT-SoVITS 组件 m_PostURL 未序列化（Inspector为空） | Unity MCP 直接设值 + 场景保存 |
| 4 | `NotifyTTSEnd` 导致句间动作提前结束 | 移除 Update() 中的 NotifyTTSEnd 调用 |
| 5 | `ShouldEnd` return true 导致动作立即回待机 | 还原为 `holdTimer >= holdAfterTTS` |
| 6 | `hasPunct` 条件短路导致短句单发 TTS | SplitForTts 纯按 ≥15 字切分 |
| 7 | `firstClip` 旗标导致对话框消失后不重开 | 改为 PlayQueueLoop 每次 dequeue 兜底 + 绝对时间 |
| 8 | 对话框句间闪烁 (`_ttsAllDispatched` 太早 true) | 改用 `_playQueue.Count > 0` 判断 |
| 9 | 对话框/动作/音频三系统不同步 | PlayQueueLoop 统一生命周期 |
| 10 | `response_queue` 容量 5 被流式 chunk 挤掉完整响应 | 扩到 50 |
| 11 | `emotionPlayer.IsPlaying` 出队条件阻塞正常消息 | 还原 + queue 扩容兜底 |
| 12 | 翻译语言码 `all_ja` 百度不认识 | 改回 `jp`；textLang 单独处理 |
| 13 | 中文文本在关闭翻译时显示两遍 | 删 GenerateVoice 的 text_answer 追加；搬入翻译回调 |
| 14 | 括号内容计入了 SplitForTts 长度 | 在 ProcessResponse 统一去括号 |
| 15 | 邦邦咔邦翻译后乱码无法命中分割 | 翻译前替换为パンパカパーン；分句前摘出 |
| 16 | `update_voice_config` 命令名不匹配 | 统一为 `update_config` |
| 17 | WebSocket 断连 WPF 不更新状态 | NetManager.OnConnectionChanged 事件 |
| 18 | Player.log / Console 没有运行时日志 | 全链路加 `[Pipeline][Split][Stream][Trans][TTS][Play][LLM]` |

## 已修复 (历史 session)

| 问题 | 修复 |
|------|------|
| 拖拽冻结 / grip 无响应 / Input 不兼容 | GetCursorPos+SetWindowPos |
| 动作 loop/表情预览/BlendShape/腮红 系列 | 全链路 loop + 预览带targets + FindBestBlendShape |
| Per-model 配置丢失/偏移 | _defaultEyeProfile 快照 + 即时恢复 |

---

## 剩余待修复

### 中

| # | 问题 | 位置 |
|---|------|------|
| 1 | 消息多时偶发卡死 (CreateRoundedBg / OnGUI 每帧 alloc) | `GameStart.cs` / `LLMFormatter.cs` |

### 轻微

| # | 问题 | 位置 |
|---|------|------|
| 2 | streamBuffer WebSocket 中断后残留 | `GameStart.cs` |
| 3 | msg_length_receive 无上限 | `GameStart.cs` |
| 4 | Connect 错误处理中 m_clientWebSocket 可能为 null | `NetManager.cs` |
| 5 | System.Random 频繁无种子创建 | `SimpleVitsApi.cs` |
