# AliceBot Bug 清单

> 2026-06-13 更新 — 动作系统 v2 重构后，本次 session 修复 9 个 bug

---

## 已修复 (本次 session)

| # | 原问题 | 修复方式 |
|---|--------|---------|
| 1 | `NetManager.response_queue` 竞态条件 | `Queue` → `ConcurrentQueue` |
| 2 | `CloseClientWebSocket` 丢弃线程引用后未 Join | 添加 `Join(1000)` 后置 null |
| 3 | `PipeServer.SendStatus` 方法体为空 | 已有完整 TCP 实现，非 bug |
| 4 | 拖拽 `WM_NCLBUTTONDOWN` 阻塞 Unity Update | 改为 `GetCursorPos + SetWindowPos` 非模态方案 |
| 5 | `OnDragStart` 仅 Ctrl 就触发 | 改为 Ctrl+MouseDown 触发 |
| 6 | 对话框 grip 无法交互 | grip resize 迁到 Update(Input 驱动)；`GetCursorPos` 坐标转换；`currentX/Y` 初始化 |
| 7 | `Input.GetMouseButton` 透明窗口下不响应 | 全局改用 `GetAsyncKeyState(0x01)` |
| 8 | Ctrl/Shift 按键下误触发触摸动画 | 触摸检测加 `!_ctrlDown && !_shiftDown` |

## 非 Bug（用户确认）

| # | 问题 | 理由 |
|---|------|------|
| — | LLMFormatter 未添加 function | 被动接收端，不应发 function |
| — | NetManager.Send 用 Binary 消息类型 | 当前实现正常，不改 |
| — | LLM 对话历史无限增长 | 被动接收端，无上下文管理 |

---

## 剩余待修复

### 中等

### 1. LLMFormatter.RemoveAction 正则表达式未正确转义
- **文件**: `LLMFormatter.cs:270`
- **现象**: 模式 `（[^\（^\）]*）` 中字符类内的 `^` 被当作字面字符处理。
- **后果**: 中文括号内的动作描述可能无法被正确剔除。

### 2. 模型切换后 BlendShape 索引不兼容
- **文件**: `ModelManager.cs`
- **现象**: 不同 VRM 模型的 BlendShape 布局不同，切换后旧表情预设 index 全错位。
- **后果**: 面部表情异常。

---

### 轻微

### 3. Connect 错误处理中 m_clientWebSocket 可能为 null
- **文件**: `NetManager.cs:70`
- **现象**: `ConnectAsync` 可能在 `m_clientWebSocket` 赋值前抛异常，此时访问 `.State` 会 NPE。

### 4. streamBuffer 在 WebSocket 中断后残留
- **文件**: `GameStart.cs:18,507-517`
- **现象**: 流式响应中断时 `streamBuffer` 残留部分数据未清理。
- **后果**: 可能污染下一条响应，导致显示异常。

### 5. msg_length_receive 无上限增长
- **文件**: `GameStart.cs:429-430`
- **现象**: `msg_length_receive` 可超过 `msg_max_length` 且无钳制。
- **后果**: UI 布局可能异常。

### 6. TransparentWindow.GetWindowPosition 回退值为 0
- **文件**: `TransparentWindow.cs:240-241`
- **现象**: 若 Win32 获取失败，返回初始值 `(0, 0)`。
- **后果**: 窗口位置可能被保存为 (0,0)。

### 7. System.Random 频繁无种子创建
- **文件**: `SimpleVitsApi.cs:70`, `GameStart.cs:72`
- **现象**: `new System.Random()` 高频创建且无种子。
- **后果**: 相同时钟精度下可能产生重复随机值。

---

## 已修复 (v2 重构时)

| # | 原问题 | 修复方式 |
|---|--------|---------|
| — | FacialController.ShyCoroutine 恢复条目重复 | FacialController 整体删除，FemaleEngine 替代 |
| — | FacialController.CryCoroutine 恢复权重错误 | 同上 |
| — | EyeTrackingController 与 FacialController 混合变形冲突 | FacialController 删除，表情/眼动由 FemaleEngine 统一管理 |
| — | ExpressionMappingManager 仅使用第一个分组 | ExpressionMappingManager 整体删除，ActionSystemRuntime 替代 |
| — | onDialogue 可能永不为 false | 新增 dialogueClearTimer + holdDuration 逻辑 |

---

## 架构备注

1. **JSON 库不统一**: `JsonUtility` / `Newtonsoft.Json` / `System.Text.Json` 混用
2. **无请求限流**: LLM 请求未有去重/限速机制
