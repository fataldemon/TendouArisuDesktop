# TODO

## 待修复 BUG

| # | 问题 | 文件 | 严重度 |
|---|------|------|:--:|
| 1 | `LLMFormatter.RemoveAction` 正则未正确转义 | `LLMFormatter.cs:270` | 中 |
| 2 | `streamBuffer` WebSocket 中断后残留 | `GameStart.cs:18,507-517` | 轻 |
| 3 | `msg_length_receive` 无上限 | `GameStart.cs:429-430` | 轻 |
| 4 | `TransparentWindow.GetWindowPosition` 回退值为 0 | `TransparentWindow.cs:240-241` | 轻 |
| 5 | `System.Random` 无种子频繁创建 | `SimpleVitsApi.cs:70`, `GameStart.cs:72` | 轻 |
| 6 | 模型切换后 BlendShape 索引不兼容 | `ModelManager.cs` | 中 |
| 7 | Connect 错误处理中 m_clientWebSocket 可能为 null | `NetManager.cs:70` | 轻 |

## 已完成 Bug 修复 (2026-06-13 session)

| # | 问题 | 修复方式 |
|---|------|---------|
| — | `NetManager.response_queue` 竞态条件 | `Queue` → `ConcurrentQueue` |
| — | `CloseClientWebSocket` 丢弃线程引用未 Join | 添加 `Join(1000)` 等待线程退出 |
| — | 拖拽 `WM_NCLBUTTONDOWN` 阻塞导致窗口冻结 | 改为 `GetCursorPos + SetWindowPos` 非阻塞方案 |
| — | `OnDragStart` 触发时机不对（Ctrl 按下就触发） | Ctrl+MouseDown 才触发 |
| — | Ctrl+拖拽窗口时误触发触摸动画 | 触摸检测加 `!_ctrlDown && !_shiftDown` |
| — | 对话框 grip 无法交互 | grip resize 从 OnGUI 迁到 Update(Input驱动)；`GetCursorPos` 屏幕坐标→窗口坐标转换 |
| — | 首帧 grip 坐标偏移（cursorWin 不准） | 初始化 `currentX/Y` 为窗口实际位置 |
| — | `Input.GetMouseButton` 透明窗口下不响应 | 全局改用 `GetAsyncKeyState(0x01)` |
| — | Y轴翻转导致 cursorWin 偏移 | `cursorWin.y` 改为 top-left 原点（`cursor.Y - currentY`） |

## 新增功能 (2026-06-13 session)

| # | 功能 |
|---|------|
| — | **Shift+左键拖拽** 平移镜头（`panSpeed = 0.006f`，`Space.Self`） |
| — | 窗口拖拽不再阻塞 Unity Update（`SetWindowPos` 非模态） |

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
