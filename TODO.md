# TODO

## 待修复 BUG

| # | 问题 | 文件 | 严重度 |
|---|------|------|:--:|
| 1 | `NetManager.response_queue` 竞态条件 | `NetManager.cs:85` | 严重 |
| 2 | `CloseClientWebSocket` 丢弃线程引用后未 Join | `NetManager.cs:157-161` | 严重 |
| 3 | `LLMFormatter` 创建 function 但未添加到列表 | `LLMFormatter.cs:180-219` | 中 |
| 4 | `NetManager.Send` 对文本用 Binary 消息类型 | `NetManager.cs:134` | 中 |
| 5 | `LLMFormatter.RemoveAction` 正则未正确转义 | `LLMFormatter.cs:270` | 中 |
| 6 | `streamBuffer` WebSocket 中断后残留 | `GameStart.cs:18,507-517` | 轻 |
| 7 | `msg_length_receive` 无上限 | `GameStart.cs:429-430` | 轻 |
| 8 | `onDialogue` 可能永不为 false | `GameStart.cs:424-438` | 轻 |
| 9 | LLM 对话历史无限增长 | `LLMFormatter.cs:248,260` | 轻 |
| 10 | `TransparentWindow.GetWindowPosition` 回退值为 0 | `TransparentWindow.cs:240-241` | 轻 |
| 11 | `System.Random` 无种子频繁创建 | `SimpleVitsApi.cs:70`, `GameStart.cs:72` | 轻 |

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| 12 | **动作预设第二期**：PartClip 多部位播放引擎 | 大 |
| 13 | **面部预设编辑器**：FacialController 硬编码 → 数据驱动 | 大 |
| 14 | **层管理**：自定义层 CRUD，Override/Additive 模式 | 中 |
| 15 | **Allow Root Motion**：预览时生效 | 小 |
