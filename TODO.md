# TODO

## 待修复 BUG

| # | 问题 | 文件 | 严重度 |
|---|------|------|:--:|
| 1 | `streamBuffer` WebSocket 中断后残留 | `GameStart.cs:18,507-517` | 轻 |
| 2 | `msg_length_receive` 无上限 | `GameStart.cs:429-430` | 轻 |
| 3 | `TransparentWindow.GetWindowPosition` 回退值为 0 | `TransparentWindow.cs:240-241` | 轻 |
| 4 | `System.Random` 无种子频繁创建 | `SimpleVitsApi.cs:70`, `GameStart.cs:72` | 轻 |
| 5 | Connect 错误处理中 m_clientWebSocket 可能为 null | `NetManager.cs:70` | 轻 |
| 6 | 消息多时偶发卡死 (CreateRoundedBg/OnGUI per-frame alloc/Regex) | `GameStart.cs`/`LLMFormatter.cs` | 中 |

## 已完成 Bug 修复 (2026-06-14 session)

| # | 问题 | 修复方式 |
|---|------|---------|
| — | 拖拽 `WM_NCLBUTTONDOWN` 阻塞 + 窗口冻结 | `GetCursorPos + SetWindowPos` 非阻塞方案 |
| — | `OnDragStart` 仅 Ctrl 就触发 | Ctrl+MouseDown 触发 |
| — | 对话框 grip 无法交互 | grip resize 迁到 Update(Input 驱动); `currentX/Y` 初始化 |
| — | `Input.GetMouseButton` 透明窗口下不响应 | 全局改用 `GetAsyncKeyState(0x01)` |
| — | Ctrl/Shift+触摸误触发 | 触摸检测加 `!_ctrlDown && !_shiftDown` |
| — | 拖拽松手误触发触摸 | `OnDragEnd` 设 `_isTouching=true` |
| — | 动作组 `loop` 参数未传递 | 全链路加 `loop` 字段 |
| — | 表情预览读旧数据 | 预览按钮带编辑中的 targets + 特效/腮红 |
| — | 导入 VRM 面部 meshRenderer 用错 | `FindBestBlendShapeRenderer` |
| — | 旧 JSON 只有 5 个表情预设 | `EnsureRequiredPresets` + 已有 profile 修复 |
| — | 拖拽/触摸中 loop 动画自动停 | `suppressAutoEnd` 机制 |
| — | 眨眼协程被 suppressed 时不终止 | `StopAllCoroutines` + reset weight |
| — | 眼球 Y 坐标反转 | ApplyEyeWeights up/down 交换 |
| — | 头部设 0 无效 (>=0 守卫) | `>0` → `>=0` |
| — | 腮红默认显示 | `ApplyBlush(null)` → enabled=false |
| — | 模型启动偏移累积 | 启动时恢复到 `_defaultModelPos/Rot` |
| — | 模型切换后默认配置丢失 | `_defaultEyeProfile` 快照 + RestoreDefault 恢复 |
| — | `LLMFormatter.RemoveAction` 正则未正确转义 | 简化为 `"（[^（）]*）"` |

## 新增功能 (2026-06-14 session)

| # | 功能 |
|---|------|
| — | **Shift+左键拖拽** 平移镜头 |
| — | 窗口拖拽非阻塞 + 命中检测 (鼠标离模型400px) |
| — | **模型独立表情配置** (per-model expression profile + VRM 自动发现 + WPF 编辑/预览/保存) |
| — | **眼部动作 per-model 配置** (BlendShape 映射 + VRM 自动检测 + 每行预览) |
| — | **对话框气泡颜色自定义** (取色盘 + 色块预览) |
| — | **每模型缩放比例** (0.1-3.0 slider + 持久化) |
| — | **模型选择持久化** (重启自动加载) |
| — | 动作组 **loop 保存** |
| — | WPF 表情预设编辑: **可交互 BlendShape 选择** (ComboBox + 删除 + 添加) |

## 已完成功能

| # | 功能 | 状态 |
|---|------|:--:|
| — | 动作系统 v2 重构 (Playable API + 数据驱动) | ✅ |
| — | 模型独立表情 + 眼部配置 | ✅ |
| — | 窗口拖拽/触摸/grip 重构 | ✅ |
| — | 表情预览实时生效 (WPF targets → mesh) | ✅ |
| — | 对话框气泡颜色 | ✅ |
| — | 每模型缩放 + 模型选择持久化 | ✅ |

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| 13 | 正常播放也支持 apply root motion | 中 |
| 14 | 多部位动画全局预览带 Mask | 中 |
| 15 | 随机事件 loop/时长优化 | 小 |
| 16 | 消息多时卡死修复 (OnGUI/RoundedBg/Regex 优化) | 中 |
| 17 | 挂载系统 (附件 + 物品库 + 持続/动作挂载) | 设计完成 |
