# Quick Response Bao 1.0.0-rc.1 发布候选验证

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
| 6 | 关于页显示 `1.0.0-rc.1` | Not Tested | | | |
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

## 通过门槛

- `checksums.txt` 中安装包和便携版两个 SHA-256 都匹配。
- Actions 的 CI 与 Release Candidate 工作流均为 Success，编译 0 警告、0 错误，全部测试通过。
- 五类目标应用的 150 项记录完成，且没有标记为阻塞正式发布的未解决问题。
- 在满足以上条件前，不创建正式 `v1.0.0` Tag 或 Release。
