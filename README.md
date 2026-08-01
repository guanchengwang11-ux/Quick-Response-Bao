# Quick Response Bao

Helping you get rid of boring customer service work.

Quick Response Bao 是一款面向 Windows 10/11 x64 的本地话术快速搜索助手。它常驻系统托盘，在白名单应用中检测当前连续输入的英文检索词，显示不抢焦点的候选窗口，并将用户确认的完整话术粘贴到原输入框。程序永远不会自动发送消息。

## 当前功能

- .NET 8 WPF、MVVM 分层解决方案
- SQLite 本地话术库及新增、编辑、复制、启停、删除和搜索
- 摘要、正文、关键词、分类的大小写不敏感匹配与优先级排序
- 全局低级键盘 Hook、进程白名单、密码控件保护和即时暂停
- 不抢焦点的候选窗、键盘/鼠标选择、全部匹配文字红色高亮
- Unicode 剪贴板粘贴、延迟恢复原剪贴板，绝不模拟发送键
- 简体中文/英文动态切换、设置持久化和双语托盘菜单
- CSV/JSON 导入与导出、公开 GitHub Release 更新检查
- 独立更新器、Inno Setup 安装配置、CI 与 Tag Release 工作流

## 系统与开发环境

- Windows 10/11 64-bit
- .NET 8 SDK；Visual Studio 2022（可选，需安装 .NET 桌面开发工作负载）
- 架构：x64

## 项目结构

```text
src/QuickResponseBao.App             WPF 界面、托盘、候选窗和交互
src/QuickResponseBao.Core            实体、接口、搜索和高亮规则
src/QuickResponseBao.Infrastructure  SQLite、设置、Hook、文件与更新服务
src/QuickResponseBao.Updater         独立文件替换与重启程序
tests/QuickResponseBao.UnitTests     单元及仓储集成测试
installer/                           Inno Setup 安装脚本
samples/                             导入模板
```

## 构建和测试

```powershell
dotnet restore QuickResponseBao.sln
dotnet build QuickResponseBao.sln --configuration Release
dotnet test QuickResponseBao.sln --configuration Release
dotnet run --project src/QuickResponseBao.App/QuickResponseBao.App.csproj
```

生成便携版：

```powershell
dotnet publish src/QuickResponseBao.App/QuickResponseBao.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

安装包由 `installer/QuickResponseBao.iss` 使用 Inno Setup 6 编译。推送 `v*` Tag 后，Release 工作流会测试、发布 x64 自包含程序、生成便携 ZIP 和安装 EXE、计算 SHA-256，并创建 GitHub Release。

## 本地数据

所有用户数据位于 `%LocalAppData%\QuickResponseBao\`：

- `data/quick-responses.db`：话术库
- `config/settings.json`：设置
- `logs/`：不含话术正文和完整输入的事件日志
- `backups/`、`updates/`：备份与更新文件

更新文件不包含上述目录，因此升级不会覆盖话术和设置。

## 隐私说明

Quick Response Bao 不会保存或上传用户的完整键盘输入、聊天内容或剪贴板内容。Hook 只在用户配置的白名单进程中维护一个最长 64 个英文字符的临时连续检索缓冲；切换应用、输入分隔字符、按 Esc、暂停监听或退出时立即清空。程序会检查 Windows 密码样式和 UI Automation 的密码属性。

话术检索默认完全在本地完成。确认候选时只模拟 `Ctrl+V`，不模拟 Enter，也不会自动发送聊天消息。更新检查仅访问本仓库的公开 GitHub Releases，无需也不存储 GitHub Token。

## 常见问题

- 候选窗未出现：确认监听为绿色、当前程序在白名单中，且连续英文字符达到设置阈值。
- 无法粘贴：目标程序若以管理员身份运行，请以相同权限运行 Quick Response Bao。
- 剪贴板未恢复：适当增大设置中的剪贴板恢复延迟。
- Hook 或数据库错误：查看 `%LocalAppData%\QuickResponseBao\logs\`，日志不会记录完整输入或话术正文。

当前仓库仍处于 V1.0.0 开发阶段；发布 Tag 前需要在 Lark、Telegram、Discord、Chrome 和 Edge 中完成手动兼容性测试。
