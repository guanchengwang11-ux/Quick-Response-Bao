# Quick Response Bao 1.0.0-rc.3 发布候选验证

## 自动验证覆盖

Actions Artifact 在干净 Windows runner 上完成 Release / win-x64 自包含发布、全部测试、Inno Setup 编译、安装、启动、覆盖升级、卸载保留数据、便携版解压启动、程序集版本、快捷方式、用户数据隔离和 SHA-256 检查。

## 安装版人工验证

| # | 操作/预期 | 状态 | 实际结果 | 截图/日志 | 备注 |
|---:|---|---|---|---|---|
| 1 | 双击 Setup，安装向导正常启动 | Not Tested | | | |
| 2 | 可浏览并选择安装目录 | Not Tested | | | |
| 3 | 勾选后创建桌面快捷方式 | Not Tested | | | |
| 4 | 安装后存在开始菜单快捷方式 | Not Tested | | | |
| 5 | 安装完成后程序正常启动 | Not Tested | | | |
| 6 | 关于页显示 `1.0.0-rc.3` | Not Tested | | | |
| 7 | 快捷方式和任务管理器中的主程序路径指向所选目录 | Not Tested | | | |
| 8 | 安装目录存在 `unins000.exe` | Not Tested | | | |
| 9 | 交互卸载会询问是否保留用户数据；分别验证“是/否” | Not Tested | | | |
| 10 | 重装/升级保留数据库、设置、备份和日志 | Not Tested | | | |
| 11 | 普通用户可安装；若策略阻止，提示应明确 | Not Tested | | | |
| 12 | 程序文件只在安装目录，用户数据只在 `%LocalAppData%\QuickResponseBao` | Not Tested | | | |

静默升级方式为 `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`，默认保留用户数据。静默卸载默认保留用户数据；只有显式增加 `/REMOVEUSERDATA` 才删除。交互卸载由用户选择。

## 便携版人工验证

| # | 操作/预期 | 状态 | 实际结果 | 截图/日志 | 备注 |
|---:|---|---|---|---|---|
| 1 | ZIP 可用 Windows 资源管理器或 7-Zip 正常解压 | Not Tested | | | |
| 2 | 解压后直接运行，无需安装 | Not Tested | | | |
| 3 | 从非开发机目录和含空格目录启动 | Not Tested | | | |
| 4 | 未安装 Visual Studio 的机器可启动 | Not Tested | | | |
| 5 | 未安装 .NET 8 的机器可启动（包为 self-contained） | Not Tested | | | |
| 6 | 放入普通用户可读但程序目录不可写的位置，数据仍写入 LocalAppData | Not Tested | | | |
| 7 | 替换便携版程序文件升级，LocalAppData 用户数据保持不变 | Not Tested | | | |
| 8 | 删除解压目录只删除程序文件，不影响其他目录或 LocalAppData 数据 | Not Tested | | | |

## RC.1 阻塞问题回归

| # | 操作/预期 | 状态 | 实际结果 | 截图/日志 | 备注 |
|---:|---|---|---|---|---|
| 1 | 在外部普通权限输入框执行“测试剪贴板粘贴”，完整粘贴且诊断显示发送 `4/4`、INPUT 大小 `40` | Not Tested | | | |
| 2 | 目标程序以管理员权限运行、Bao 以普通权限运行时，出现明确权限级别提示且不误报成功 | Not Tested | | | |
| 3 | 粘贴成功后原剪贴板按设置恢复；发送失败时不提前执行恢复逻辑 | Not Tested | | | |
| 4 | Lark 真实前台进程可通过倒计时捕获并一键加入白名单 | Not Tested | | | |
| 5 | Lark 的 UI Automation 状态为 Unknown/Unavailable 且无密码证据时仍可产生检索候选 | Not Tested | | | |
| 6 | 原生密码框或 UIA `IsPassword=true` 时不记录检索缓冲，候选框不出现 | Not Tested | | | |
| 7 | Lark 无 Caret 时依次降级到当前窗口右下角、当前显示器右下角，并在诊断页显示方式 | Not Tested | | | |
| 8 | 输入 `how` 长度为 3 不触发；输入 `how ` 长度为 4 并匹配 `how to ...` | Not Tested | | | |
| 9 | `solve   this` 将连续空格规范化并匹配 `solve this`；Backspace、Esc、Enter、切换应用按规则重置 | Not Tested | | | |
| 10 | 诊断页在中英文及 100%/125%/150%/175% 缩放下无行重叠，长路径可选中复制 | Not Tested | | | |
| 11 | 首页、话术库、分类、导入导出、白名单、诊断、设置、关于八页在浅/深色下布局与交互正常 | Not Tested | | | |
| 12 | Lark、Telegram、Discord、Chrome、Edge 均完成自动粘贴且不会自动发送消息 | Not Tested | | | |

## RC.2 重复触发文字回归

以下项目分别在 Lark、Telegram、Discord、Chrome 和 Edge 执行，并覆盖 Enter、Tab 与鼠标确认。

| # | 操作/预期 | 状态 | 实际结果 | 截图/日志 | 备注 |
|---:|---|---|---|---|---|
| 1 | 输入 `how to`，选择正文 `how to solve this problem`，最终仅出现一次完整正文 | Not Tested | | | |
| 2 | 输入 `how  to`（两个空格），搜索按 `how to` 匹配，但实际删除 7 个字符 | Not Tested | | | |
| 3 | 输入 `how `（末尾空格），末尾空格随触发文字一起删除，不产生额外空格 | Not Tested | | | |
| 4 | 候选出现后继续输入或 Backspace，诊断中的删除字符数与实际输入同步 | Not Tested | | | |
| 5 | 候选出现后按 Left/Right/Home/End 或点击输入框其他位置，旧候选取消且不删除其他内容 | Not Tested | | | |
| 6 | 候选出现后切换窗口/应用或关闭目标程序，取消替换并显示明确提示 | Not Tested | | | |
| 7 | 模拟删除注入失败，确认不继续粘贴；模拟粘贴失败，确认尝试恢复原触发文字 | Not Tested | | | |
| 8 | Enter 确认不发送消息，Tab 确认不插入 Tab，鼠标确认结果与键盘一致 | Not Tested | | | |
| 9 | 原剪贴板在成功替换后恢复，中文、英文及多行正文均完整 | Not Tested | | | |
| 10 | 关闭“插入话术时替换检索文字”后，恢复在当前光标处直接插入的旧模式；重启后设置保留 | Not Tested | | | |

## 通过门槛

- `checksums.txt` 中安装包和便携版两个 SHA-256 都匹配。
- Actions 的 CI 与 Release Candidate 工作流均为 Success，编译 0 警告、0 错误，全部测试通过。
- 五类目标应用的 150 项记录完成，且没有标记为阻塞正式发布的未解决问题。
- 在满足以上条件前，不创建正式 `v1.0.0` Tag 或 Release。
