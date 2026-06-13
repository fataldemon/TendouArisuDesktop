# AliceBot Bug 清单

> 2026-06-13 更新 — 动作系统 v2 重构后

---

## 严重

### 1. NetManager.response_queue 竞态条件
- **文件**: `NetManager.cs:85`, `GameStart.cs:467`
- **现象**: 后台线程 `ReceiveData` 无锁向 `Queue<string>` 入队，主线程 `Update()` 无锁出队。`Queue<T>` 非线程安全。
- **后果**: `InvalidOperationException`（队列为空时 Dequeue）或内部状态损坏导致数据丢失。

### 2. CloseClientWebSocket 丢弃线程引用后未 Join
- **文件**: `NetManager.cs:157-161`
- **现象**: 设置 `m_dataReceiveThread = null` 前未调用 `Join()` 等待线程结束。
- **后果**: 线程可能仍在运行并访问已释放的 socket，导致未定义行为。

---

## 中等

### 3. PipeServer.SendStatus 方法体为空
- **文件**: `PipeServer.cs:245-247`
- **现象**: 方法被调用以通知 WPF WebSocket 连接状态变化，但方法体为 `{ }`。
- **后果**: WPF 设置面板永远无法获知连接状态变化。

### 4. LLMFormatter 创建 function 但未添加到列表
- **文件**: `LLMFormatter.cs:180-219`
- **现象**: `search_for_item` 和 `search_on_internet` 被创建为局部对象但**从未**添加到 `functions`。
- **后果**: LLM 永远不知道这两个 function 存在，功能不可用。

### 5. NetManager.Send 对文本数据使用 Binary 消息类型
- **文件**: `NetManager.cs:134`
- **现象**: `SendAsync(array, WebSocketMessageType.Binary, ...)` — JSON 文本应以 `WebSocketMessageType.Text` 发送。
- **后果**: 严格 WebSocket 实现可能拒绝 Binary 类型的文本帧。

### 6. LLMFormatter.RemoveAction 正则表达式未正确转义
- **文件**: `LLMFormatter.cs:270`
- **现象**: 模式 `（[^\（^\）]*）` 中字符类内的 `^` 被当作字面字符处理。
- **后果**: 中文括号内的动作描述可能无法被正确剔除。

---

## 轻微

### 7. Connect 错误处理中 m_clientWebSocket 可能为 null
- **文件**: `NetManager.cs:70`
- **现象**: `ConnectAsync` 可能在 `m_clientWebSocket` 赋值前抛异常，此时访问 `.State` 会 NPE。

### 8. streamBuffer 在 WebSocket 中断后残留
- **文件**: `GameStart.cs:18,507-517`
- **现象**: 流式响应中断时 `streamBuffer` 残留部分数据未清理。
- **后果**: 可能污染下一条响应，导致显示异常。

### 9. msg_length_receive 无上限增长
- **文件**: `GameStart.cs:429-430`
- **现象**: `msg_length_receive` 可超过 `msg_max_length` 且无钳制。
- **后果**: UI 布局可能异常。

### 10. LLM 对话历史无限增长
- **文件**: `LLMFormatter.cs:248,260`
- **现象**: `history` 列表和 `formatted_history` 字符串无限追加。
- **后果**: WebSocket 消息体过大，可能导致连接失败或内存溢出。

### 11. TransparentWindow.GetWindowPosition 回退值为 0
- **文件**: `TransparentWindow.cs:240-241`
- **现象**: 若 Win32 获取失败，返回初始值 `(0, 0)`。
- **后果**: 窗口位置可能被保存为 (0,0)。

### 12. System.Random 频繁无种子创建
- **文件**: `SimpleVitsApi.cs:70`, `GameStart.cs:72`
- **现象**: `new System.Random()` 高频创建且无种子。
- **后果**: 相同时钟精度下可能产生重复随机值。

### 13. 模型切换后 BlendShape 索引不兼容
- **文件**: `ModelManager.cs`
- **现象**: 不同 VRM 模型的 BlendShape 布局不同，切换后旧表情预设 index 全错位。
- **后果**: 面部表情异常。

---

## 已修复 (v2)

| # | 原问题 | 修复方式 |
|---|--------|---------|
| — | FacialController.ShyCoroutine 恢复条目重复 (#3) | FacialController 整体删除，FemaleEngine 替代 |
| — | FacialController.CryCoroutine 恢复权重错误 (#4) | 同上 |
| — | EyeTrackingController 与 FacialController 混合变形冲突 (#8) | FacialController 删除，表情/眼动由 FemaleEngine 统一管理 |
| — | ExpressionMappingManager 仅使用第一个分组 (#10) | ExpressionMappingManager 整体删除，ActionSystemRuntime 替代 |
| — | onDialogue 可能永不为 false (#14) | 新增 dialogueClearTimer + holdDuration 逻辑 |

---

## 架构备注

1. **JSON 库不统一**: `JsonUtility` / `Newtonsoft.Json` / `System.Text.Json` 混用
2. **Bool 状态标志简化**: v2 重构后去掉了 `withExpression` / `onRestore` / `facialRestoreTimer`，但仍保留部分
3. **无请求限流**: LLM 请求未有去重/限速机制
