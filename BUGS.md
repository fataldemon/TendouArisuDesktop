# AliceBot Bug 清单

> 2026-06-14 — 本次 session 修复 18 个 bug, 新增 7 个功能

---

## 已修复 (本次 session)

| # | 问题 | 修复方式 |
|---|------|---------|
| 1 | 拖拽 `WM_NCLBUTTONDOWN` 阻塞导致窗口冻结 | `GetCursorPos + SetWindowPos` 非模态方案 |
| 2 | `OnDragStart` 仅 Ctrl 就触发 | Ctrl+MouseDown 触发 |
| 3 | 对话框 grip 无法交互 | grip resize 迁到 Update(Input驱动); `currentX/Y` 初始化 |
| 4 | `Input.GetMouseButton` 透明窗口下不响应 | 全局改用 `GetAsyncKeyState(0x01)` |
| 5 | Ctrl/Shift+触摸误触发 | 触摸检测加 `!_ctrlDown && !_shiftDown` |
| 6 | 拖拽松手误触发触摸 | `OnDragEnd` 设 `_isTouching=true` |
| 7 | 动作组 `loop` 参数全链路未传递 | WPF→PipeCmd→Runtime 加 `loop` 字段 |
| 8 | 表情预览读旧数据 + 丢失特效/腮红 | WPF发targets + 查表补activateObjects/blushMode |
| 9 | 面部 BlendShape 写到身体 mesh | `FindBestBlendShapeRenderer` |
| 10 | 旧 JSON 只有 5 个表情预设 | `EnsureRequiredPresets` + 已有 profile 修复 |
| 11 | 拖拽/触摸中 loop 动画超时停 | `suppressAutoEnd` 机制 |
| 12 | 眨眼协程被 suppressed 时不终止 | `StopAllCoroutines` + reset weight |
| 13 | 眼球 Y 坐标反转 | ApplyEyeWeights up/down 交换 |
| 14 | 头部设 0 无效 | `> 0` → `>= 0` |
| 15 | 腮红默认显示 | `ApplyBlush(null)` → blushRenderers.enabled=false |
| 16 | 模型启动偏移累积 | 启动恢复到 `_defaultModelPos/Rot` |
| 17 | 模型切换后默认眼部配置丢失 | `_defaultEyeProfile` 快照 |
| 18 | `LLMFormatter.RemoveAction` 正则转义 | 简化为 `"（[^（）]*）"` |

## 非 Bug（用户确认）

| # | 问题 | 理由 |
|---|------|------|
| — | LLMFormatter 未添加 function | 被动接收端 |
| — | NetManager.Send 用 Binary 消息类型 | 当前实现正常 |
| — | LLM 对话历史无限增长 | 被动接收端 |

---

## 剩余待修复

### 中

### 1. 消息多时偶发卡死
- **位置**: `GameStart.cs:770/792`, `LLMFormatter.cs:285`
- **主因**: `CreateRoundedBg` 每帧重建 16MB heap + GPU upload; `new GUIStyle`/`new RectOffset` 每帧分配
- **次因**: `RemoveEmotion` 未编译 Regex 每帧创建

### 2. 模型切换后 BlendShape 索引不兼容
- **位置**: `ModelManager.cs`
- **现象**: 不同 VRM 模型 BlendShape 布局不同。V3 已通过 per-model profile + VRM 自动检测 + 手动编辑覆盖解决

---

### 轻微

### 3. Connect 错误处理中 m_clientWebSocket 可能为 null
### 4. streamBuffer WebSocket 中断后残留
### 5. msg_length_receive 无上限增长
### 6. GetWindowPosition 回退值为 0
### 7. System.Random 频繁无种子创建

---

## 已修复 (v2 重构时)

| # | 原问题 | 修复方式 |
|---|--------|---------|
| — | FacialController 恢复条目重复/权重错误 | FacialController 删除, FemaleEngine 替代 |
| — | EyeTrackingController 与 FacialController 混合变形冲突 | FacialController 删除 |
| — | ExpressionMappingManager 仅使用第一个分组 | ActionSystemRuntime 替代 |
| — | onDialogue 可能永不为 false | dialogueClearTimer + holdDuration 逻辑 |
| — | NetManager.response_queue 竞态条件 | Queue → ConcurrentQueue |
| — | CloseClientWebSocket 线程未 Join | Join(1000) |

---

## 待实现功能

| # | 功能 | 复杂度 |
|---|------|:--:|
| — | 正常播放 apply root motion | 中 |
| — | 多部位动画全局预览带 Mask | 中 |
| — | 挂载系统 (物品库+持続/动作挂载) | 设计完成, feature/attachment-system |
