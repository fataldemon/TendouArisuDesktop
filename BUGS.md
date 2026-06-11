# AliceBot Bug 清单

> 2025-06-11 代码审查发现

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

### 3. FacialController.ShyCoroutine 重复的 mouthAngryBlendIndex 恢复条目
- **文件**: `FacialController.cs:461-464`
- **现象**: `mouthAngryBlendIndex` 被添加了两次（50f 和 35f），第二次本应为 `mouthIBlendIndex`（匹配第453行实际应用的值）。
- **后果**: 表情回滚时 mouthI 混合变形不会被重置，面部残留异常。

### 4. FacialController.CryCoroutine 恢复权重错误
- **文件**: `FacialController.cs:415 vs 421-422`
- **现象**: mouthI 实际应用权重为 `weight*0.8f`（80%），但记录的恢复权重为 `50f`。
- **后果**: 哭泣表情回滚时 mouthI 无法完全恢复。

---

## 中等

### 5. PipeServer.SendStatus 方法体为空
- **文件**: `PipeServer.cs:245-247`
- **现象**: 方法被调用以通知 WPF WebSocket 连接状态变化，但方法体为 `{ }`。
- **后果**: WPF 设置面板永远无法获知连接状态变化。

### 6. LLMFormatter 创建 function 但未添加到列表
- **文件**: `LLMFormatter.cs:180-219`
- **现象**: `search_for_item` 和 `search_on_internet` 被创建为局部对象但**从未**添加到 `functions`。
- **后果**: LLM 永远不知道这两个 function 存在，功能不可用。

### 7. NetManager.Send 对文本数据使用 Binary 消息类型
- **文件**: `NetManager.cs:134`
- **现象**: `SendAsync(array, WebSocketMessageType.Binary, ...)` — JSON 文本应以 `WebSocketMessageType.Text` 发送。
- **后果**: 严格 WebSocket 实现可能拒绝 Binary 类型的文本帧。

### 8. EyeTrackingController 与 FacialController 混合变形冲突
- **文件**: `EyeTrackingController.cs:92-96`, `FacialController.cs:157-158`
- **现象**: EyeTracking 每帧设置 lookUp/Down/Left/Right。但 "shy" 等表情也设置 lookDown + lookLeft。同时运行时 EyeTracking 会覆盖表情值。
- **后果**: 害羞等表情的眼神方向被眼动追踪覆盖。

### 9. LLMFormatter.RemoveAction 正则表达式未正确转义
- **文件**: `LLMFormatter.cs:270`
- **现象**: 模式 `（[^\（^\）]*）` 中字符类内的 `^` 被当作字面字符处理。
- **后果**: 中文括号内的动作描述可能无法被正确剔除。

### 10. ExpressionMappingManager 仅使用第一个分组
- **文件**: `ExpressionMappingManager.cs:64,76`
- **现象**: `TryApplyFacial` 只取 `facialGroups[0]`，`TryApplyAction` 只取 `actionGroups[0]`。
- **后果**: 多重表情/动作映射配置完全无效。

---

## 轻微

### 11. Connect 错误处理中 m_clientWebSocket 可能为 null
- **文件**: `NetManager.cs:70`
- **现象**: `ConnectAsync` 可能在 `m_clientWebSocket` 赋值前抛异常，此时访问 `.State` 会 NPE。

### 12. streamBuffer 在 WebSocket 中断后残留
- **文件**: `GameStart.cs:18,507-517`
- **现象**: 流式响应中断时 `streamBuffer` 残留部分数据未清理。
- **后果**: 可能污染下一条响应，导致显示异常。

### 13. msg_length_receive 无上限增长
- **文件**: `GameStart.cs:429-430`
- **现象**: `msg_length_receive` 可超过 `msg_max_length` 且无钳制。
- **后果**: UI 布局可能异常。

### 14. onDialogue 可能永不为 false
- **文件**: `GameStart.cs:424-438`
- **现象**: 若 `getWaitingStatus()` 从不返回 true，对话气泡永远渲染。
- **后果**: 空闲时 UI 残留。

### 15. LLM 对话历史无限增长
- **文件**: `LLMFormatter.cs:248,260`
- **现象**: `history` 列表和 `formatted_history` 字符串无限追加。
- **后果**: WebSocket 消息体过大，可能导致连接失败或内存溢出。

### 16. TransparentWindow.GetWindowPosition 回退值为 0
- **文件**: `TransparentWindow.cs:240-241`
- **现象**: 若 Win32 获取失败，返回初始值 `(0, 0)`。
- **后果**: 窗口位置可能被保存为 (0,0)。

### 17. System.Random 频繁无种子创建
- **文件**: `SimpleVitsApi.cs:70`, `GameStart.cs:72`
- **现象**: `new System.Random()` 高频创建且无种子。
- **后果**: 相同时钟精度下可能产生重复随机值。

---

## 架构备注

1. **JSON 库不统一**: `JsonUtility` / `Newtonsoft.Json` / `System.Text.Json` 混用
2. **Bool 状态标志过多**: `withExpression` / `onRestore` / `onVoice` / `onDialogue` 等组合逻辑复杂易错
3. **无请求限流**: LLM 请求未有去重/限速机制
