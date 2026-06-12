# TODO

## 待修复 BUG

| # | 问题 | 文件 | 严重度 |
|---|------|------|:--:|
| 1 | `NetManager.response_queue` 竞态条件 | `NetManager.cs:85` | 严重 |
| 2 | `LLMFormatter` 创建 function 但未添加到列表 | `LLMFormatter.cs:180-219` | 中 |
| 3 | `NetManager.Send` 对文本用 Binary 消息类型 | `NetManager.cs:134` | 中 |
| 4 | `LLMFormatter.RemoveAction` 正则未正确转义 | `LLMFormatter.cs:270` | 中 |
| 5 | `streamBuffer` WebSocket 中断后残留 | `GameStart.cs:18,507-517` | 轻 |
| 6 | `msg_length_receive` 无上限 | `GameStart.cs:429-430` | 轻 |
| 7 | `onDialogue` 可能永不为 false | `GameStart.cs:424-438` | 轻 |
| 8 | LLM 对话历史无限增长 | `LLMFormatter.cs:248,260` | 轻 |
| 9 | `TransparentWindow.GetWindowPosition` 回退值为 0 | `TransparentWindow.cs:240-241` | 轻 |
| 10 | `System.Random` 无种子频繁创建 | `SimpleVitsApi.cs:70`, `GameStart.cs:72` | 轻 |
| 11 | 拖拽 `OnDragStart` 触发时机不对（Ctrl 按下就触发，应该是 Ctrl+MouseDown） | `TransparentWindow.cs:180` | 轻 |

## 已完成功能

| # | 功能 | 状态 |
|---|------|:--:|
| — | 情绪映射简化（单分组 + weight 应用） | ✅ |
| — | 头部平滑 blend-out + eye tracking switch 修复 | ✅ |
| — | 对话框 grip 移动位置 + 宽高 WPF 设置 | ✅ |
| — | TCP 重连鲁棒性（防火墙 + `ReadTimeout` + 退出优化） | ✅ |
| — | 动作预设第一期（预设名↔actionParam + WPF 编辑器 + 迁移） | ✅ |
| — | 触摸/拖拽事件（情绪映射映射链路 + 面部联动 + 恢复） | ✅ |
| — | `onAction` 由 Animator 状态机驱动（不再手动设） | ✅ |
| — | 表情先清再设（消除 idle→动作表情叠加） | ✅ |
| — | 全局预览 `reset_blendshapes`（不启动待机协程） | ✅ |
| — | WPF 重复打开 → 切到老窗口（Mutex + WM_COPYDATA） | ✅ |
| — | 待机→随机待机头动过渡平滑 | ✅ |

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| 12 | **统一 PlayEmotion 入口**（等动作系统重做后一起改） | 中 |
| 13 | **动作预设第二期**：PartClip 多部位播放引擎 | 大 |
| 14 | **面部预设编辑器**：FacialController 硬编码 → 数据驱动 | 大 |
| 15 | **眨眼抑制**（动作/预览/触摸时不眨眼） | 小 → 待定 |
