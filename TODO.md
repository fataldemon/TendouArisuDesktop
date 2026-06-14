# TODO

## 待修复 BUG

| # | 问题 | 文件 | 严重度 |
|---|------|------|:--:|
| 1 | `streamBuffer` WebSocket 中断后残留 | `GameStart.cs:18,507-517` | 轻 |
| 2 | `msg_length_receive` 无上限 | `GameStart.cs:429-430` | 轻 |
| 3 | `TransparentWindow.GetWindowPosition` 回退值为 0 | `TransparentWindow.cs:240-241` | 轻 |
| 4 | `System.Random` 无种子频繁创建 | `SimpleVitsApi.cs:70`, `GameStart.cs:72` | 轻 |
| 5 | Connect 错误处理中 m_clientWebSocket 可能为 null | `NetManager.cs:70` | 轻 |
| 6 | `BlinkController` 眼部 BlendShape index 硬编码，导入 VRM 不兼容 | `BlinkController.cs`, `ModelManager.cs` | 中 |

## 已完成 Bug 修复 (2026-06-14 session)

| # | 问题 | 修复方式 |
|---|------|---------|
| — | `LLMFormatter.RemoveAction` 正则未正确转义 | 简化为 `"（[^（）]*）"` |
| — | 拖拽 `WM_NCLBUTTONDOWN` 阻塞 + 窗口冻结 | 改为 `GetCursorPos + SetWindowPos` 非阻塞 |
| — | `OnDragStart` 仅 Ctrl 就触发 | Ctrl+MouseDown 触发 |
| — | 对话框 grip 无法交互 | grip resize 迁到 Update(Input驱动); `currentX/Y` 初始化 |
| — | `Input.GetMouseButton` 透明窗口下不响应 | 全局改用 `GetAsyncKeyState(0x01)` |
| — | Ctrl/Shift+触摸误触发 | 触摸检测加 `!_ctrlDown && !_shiftDown` |
| — | 动作组 `loop` 参数未传递 | 全链路加 `loop` 字段传递 |
| — | 表情预览读旧数据 | 预览按钮带编辑中的 targets |
| — | 导入 VRM 面部 meshRenderer 用错 | `FindBestBlendShapeRenderer` 找 BlendShape 最多 |
| — | 旧 JSON 只有 5 个表情预设 | `EnsureRequiredPresets` + 已有 profile 也调用 |
| — | 拖拽中 loop 动画超时停 + 释放立即停 | `suppressAutoEnd` 机制 |
| — | 拖拽松手误触发触摸 | `OnDragEnd` 设 `_isTouching=true` |
| — | 眨眼协程被 suppressed 时不终止 | `StopAllCoroutines` + reset weight |

## 新增功能 (2026-06-13~14 session)

| # | 功能 |
|---|------|
| — | **Shift+左键拖拽** 平移镜头 |
| — | 窗口拖拽不再阻塞 Unity Update（`SetWindowPos` 非模态） |
| — | **模型独立表情配置** (per-model expression profile + VRM 自动发现 + WPF 可编辑保存) |

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
| 14 | `BlinkController` per-model 眼部 BlendShape 列表 (从 VRM metadata 自动提取) | 中 |
| 15 | 多部位动画全局预览带 Mask (目前预览用 fullBody) | 中 |
| 16 | 随机事件 loop/时长优化 | 小 |
| 17 | 拖拽/触摸动画加速诊断 | 待调查 |
