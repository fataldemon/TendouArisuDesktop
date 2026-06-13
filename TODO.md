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
| 12 | 模型切换后 BlendShape 索引不兼容 | `ModelManager.cs` | 中 |

## 已完成功能

| # | 功能 | 状态 |
|---|------|:--:|
| — | 动作系统 v2 重构 (Playable API + 数据驱动) | ✅ |
| — | 统一 PlayEmotion 入口 | ✅ |
| — | PartClip 多部位播放引擎 (6层独立) | ✅ |
| — | 表情预设编辑器 (数据驱动 FemaleEngine 替代 15 硬编码协程) | ✅ |
| — | 眨眼抑制 (动作/预览/触摸时) | ✅ |
| — | 全局预览 (表情+多部位+ARM+ET 组合) | ✅ |
| — | 情绪映射 WPF 编辑器 (表情覆盖 + 权重 + 随机事件标记) | ✅ |
| — | 动作组 WPF 编辑器 (多部位 clip + ARM + ET + 搜索) | ✅ |
| — | 表情预设 WPF 查看器 | ✅ |
| — | Apply Root Motion 按动作组独立控制 | ✅ |
| — | Eye Tracking 按动作组独立控制 | ✅ |
| — | 对话框最短保持时间可配 (音频+最短10s) | ✅ |
| — | Layer FadeIn/FadeOut 平滑过渡 | ✅ |
| — | Crossfade 中 BlendingOut 转场保护 | ✅ |
| — | 循环动画 Loop 修复 | ✅ |
| — | 旧 Animator Controller 移除 (Playable API 替代) | ✅ |
| — | 动作组新建/删除 | ✅ |
| — | 双击列表编辑 | ✅ |
| — | 情绪映射简化（单分组 + weight 应用） | ✅ |
| — | 头部平滑 blend-out + eye tracking switch 修复 | ✅ |
| — | 对话框 grip 移动位置 + 宽高 WPF 设置 | ✅ |
| — | TCP 重连鲁棒性 | ✅ |
| — | 动作预设第一期 | ✅ |
| — | 触摸/拖拽事件 | ✅ |
| — | WPF 重复打开 → 切到老窗口 | ✅ |
| — | 托盘菜单文本同步 | ✅ |

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| 13 | 正常播放也支持 apply root motion (目前仅预览模式生效) | 中 |
| 14 | 模型切换后 BlendShape 预设兼容 | 大 |
| 15 | 多部位动画全局预览带 Mask (目前预览用 fullBody) | 中 |
| 16 | 随机事件 loop/时长优化 | 小 |
