---
name: fix-error
description: 当用户提到"报错、异常、崩溃、无法运行、构建失败、build error"时使用，修复 FireDetectionSystem（WPF/Prism/.NET 8）中的编译错误、运行异常、对话框绑定问题与用户管理模块故障
---

按以下步骤处理，不跳步：

1. 收集错误信息
- 记录原始错误文本、堆栈、触发入口（如 `UserManagement`、`UserEditDialog`、`LoginView`）
- 如果用户只给现象，先让用户补充一次最小复现步骤

2. 先做可重复验证
- 优先运行：`dotnet build FireDetectionSystem/FireDetectionSystem.csproj`
- 若构建通过但运行异常，定位到对应 `View`/`ViewModel`/`Service` 的最小路径

3. 定位根因（本项目优先检查）
- Prism 对话框：
  - `App.xaml.cs` 中 `RegisterDialog` 名称
  - `ViewModel` 内 `ShowDialog` 调用名称是否一致
- XAML 绑定：
  - `Text="{Binding ...}"`、`Command="{Binding ...}"`、`Visibility` 转换器键是否存在
- 用户管理链路：
  - `UserManagementViewModel` 参数传递
  - `UserService` 数据写入/更新逻辑
  - 角色/状态魔法字符串（`Admin`、`Operator`、`Viewer`）

4. 实施修复
- 只改必要文件，避免无关重构
- 保持现有 MVVM + Prism + Service 分层
- 涉及风险命令（如 hard reset）必须先确认

5. 验证修复
- 必做：重新运行 `dotnet build FireDetectionSystem/FireDetectionSystem.csproj`
- 若改动涉及对话框行为，补充手工验证步骤
- 当前仓库无独立测试项目时，明确写明"未执行自动化测试"

6. 输出结果
- `Root Cause`：一句话说明根因
- `Fix Applied`：列出关键修改
- `Build Validation`：构建是否通过
- `Residual Risk`：剩余风险与后续建议
