<div align="center">

**简体中文** · [English](README.en.md)

<img src="Assets/AppIcon/AppIcon-1024.png" width="128" alt="Snug 图标">

# Snug

**让 Mac 在手机上拥有原生级的操作体验**

为 UU远程 打造<br>竖屏原生显示 · 原生手势 · App 资源库 · 适配所有手机

[![最新版本](https://img.shields.io/github/v/release/zephyr4123/snug?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=blue)](https://github.com/zephyr4123/snug/releases/latest) ![Apple Silicon · Intel](https://img.shields.io/badge/Apple%20Silicon%20%C2%B7%20Intel-%E9%80%9A%E7%94%A8-success) ![macOS 14+](https://img.shields.io/badge/macOS-14%2B-blue) ![已公证](https://img.shields.io/badge/Apple-%E5%B7%B2%E5%85%AC%E8%AF%81-black?logo=apple) [![许可证 MIT](https://img.shields.io/badge/%E8%AE%B8%E5%8F%AF%E8%AF%81-MIT-green)](LICENSE)

[下载](#-下载) · [原生体验](#-原生体验) · [App 资源库](#-app-资源库) · [适配](#-适配所有-app-与手机) · [对比](#-与其他方案的对比) · [安装](#-安装) · [使用](#-使用方式) · [限制](#-已知限制) · [开发](#-开发)

<br>

<img src="https://dl.zephyrxiang.com/snug/media/phone-app-library.865fc506.png" width="30%" alt="App 资源库：运行中的 App 与各自的窗口"> <img src="https://dl.zephyrxiang.com/snug/media/phone-demo.2cee4e81.webp" width="30%" alt="实机演示：打开窗口、弹出键盘输入命令、恢复会话、单指滚动、回到列表"> <img src="https://dl.zephyrxiang.com/snug/media/phone-context-menu.997dd70f.png" width="30%" alt="长按调出右键菜单">
<p><sub>App 资源库 · 实机演示 · 长按调出右键菜单</sub><br><sub>▶ <a href="https://dl.zephyrxiang.com/snug/media/phone-demo.a97e46d1.mp4">观看高清演示视频</a></sub></p>

</div>

> 承接自 [aoyamashou/UUyc-companion](https://github.com/aoyamashou/UUyc-companion)（UU远程伴侣，作者 AoyamaShou，MIT 许可），完整保留原作者的版权声明与提交历史。2.0 起围绕「手机屏」重新设计。

## 📥 下载

<img src="Assets/AppIcon/AppIcon-1024.png" width="88" align="left" alt="Snug 图标">

**Snug for macOS** [![最新版本](https://img.shields.io/github/v/release/zephyr4123/snug?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=blue)](https://github.com/zephyr4123/snug/releases/latest)<br>一个安装包同时支持 Apple 芯片与 Intel，适用于 macOS 14 及更新版本<br>已签名并通过 Apple 公证，下载后拖入「应用程序」即可

<br clear="left">

| 渠道 | 说明 | 下载 |
| :--- | :--- | :--- |
| **国内源** | 腾讯云 CDN 加速，中国大陆推荐 | [Snug.dmg](https://dl.zephyrxiang.com/snug/latest/Snug.dmg) |
| **GitHub Releases** | 附更新说明、校验文件与历史版本 | [前往下载](https://github.com/zephyr4123/snug/releases/latest) |

两个渠道提供的是同一个安装包。

## ✨ 原生体验

UU远程 将整块桌面等比压缩进手机屏幕，文字与控件随之缩小数倍。Snug 为手机增加一块专属竖屏，Mac 的窗口在其中以手机的尺度呈现，看起来、用起来都如同手机上的原生 App。

<p align="center"><img src="https://dl.zephyrxiang.com/snug/media/compare-uu-original.cf4158d1.png" width="36%" alt="UU远程 原版：整块桌面缩在手机屏幕里"> <img src="https://dl.zephyrxiang.com/snug/media/phone-claude-code.93e21d9d.png" width="36%" alt="搭配 Snug：窗口铺满整屏，字号放大"><br><sub>同一段内容 · 左：UU远程 原版 · 右：搭配 Snug</sub></p>

### 竖屏原生显示

- **专属竖屏**：手机连接后，UU远程 的屏幕列表中多出一块「Snug 手机屏」，比例与手机一致，2 倍高清
- **窗口铺满**：选中的窗口独占整屏
- **字号放大**：Ghostty、终端、iTerm2、VS Code、Cursor、Zed 自动放大字号，按 App 分别记忆

### 原生手势

UU远程 手机端沿用触控板的手势逻辑；Snug 在手机屏上按手机的使用习惯重新解释。

| 手机上的操作 | UU远程 原版 | Snug 手机屏 |
| :--- | :--- | :--- |
| 单指滑动 | 鼠标拖拽，容易误选文字 | 页面滚动，松手带惯性 |
| 长按后拖动 | 鼠标拖拽 | 拖选文字 |
| 长按松开 | 普通点击 | 调出右键菜单 |

- 仅在手机屏内生效；其他显示器与本机的触控板、鼠标不受影响

### 与 iPhone 一致的操作

| iPhone | Snug 手机屏 |
| :--- | :--- |
| App 资源库 | [App 资源库](#-app-资源库)：首页，查找、打开与管理 Mac 上的 App |
| 主屏幕指示条 | 底部指示条，返回 App 资源库 |
| 左上角「‹ 返回」 | 左上角「‹ App 名」，返回当前窗口 |

## 📱 App 资源库

手机屏的首页，用于在手机上管理 Mac 的 App：查找、打开、切换、新建、关闭与退出，都不必回到电脑前。

- **运行中**：正在运行的 App 各占一张卡片，窗口列在各自的 App 之下。窗口无论位于哪块屏幕、哪个桌面空间，处于全屏还是最小化，均可在此找到
- **全部**：本机所有 App 的图标网格，按名称排序；尚未运行的 App 轻点即打开
- **搜索**：匹配 App 名称与窗口标题，支持中文、英文、拼音全拼与首字母，如「bwl」即可找到「备忘录」

<p align="center"><img src="https://dl.zephyrxiang.com/snug/media/phone-app-library.865fc506.png" width="30%" alt="运行中：放在手机屏上的窗口所属的 App 单列在最上方"> <img src="https://dl.zephyrxiang.com/snug/media/phone-all-apps.7d4ada60.png" width="30%" alt="全部：本机所有 App 的图标网格"> <img src="https://dl.zephyrxiang.com/snug/media/phone-search.d365e194.png" width="30%" alt="搜索：输入 bwl 找到备忘录"><br><sub>运行中 · 全部 · 搜索拼音首字母</sub></p>

| 操作 | 方式 |
| :--- | :--- |
| 打开窗口 | 轻点窗口，即铺满手机屏；轻点 App，打开其最近使用的窗口 |
| 新建窗口 | 轻点 App 卡片上的「新建窗口」，新窗口直接按手机宽度打开 |
| 关闭窗口 | 轻点窗口右侧的关闭按钮，再点一次确认 |
| 退出 App | 轻点 App 卡片上的「退出」，再点一次确认；10 秒仍未退出时，可强制退出 |
| 存储未保存的内容 | 关闭或退出时弹出的「是否存储」对话框会出现在手机屏上，在手机上即可选择 |
| 退出手机屏 | 轻点右上角的「退出手机屏」，再点一次确认；窗口归位、手机屏移除，本次远程不再显示，下次远程时恢复（断开后 15 秒内重新连接仍算本次） |

## 📐 适配所有 App 与手机

### 任意 App 都能使用、完整铺满

Snug 放上手机屏的是 Mac 上的窗口本身，任何 App 都能直接使用，不绑定任何一家 AI：终端里的 Claude Code、ChatGPT 桌面版里的 Codex，用起来与在电脑前一样。

部分 App 限定了最小宽度，无法缩到手机屏的宽度。Snug 先按手机宽度铺开窗口；App 不允许再窄时，将手机屏切换到恰好容纳它的分辨率，再次铺满。

- 任何窗口都完整显示，不会只露出一部分
- 画面等比缩放，不变形，点按位置保持准确
- 此类窗口的文字略小，可在手机上双指放大

<p align="center"><img src="https://dl.zephyrxiang.com/snug/media/phone-chatgpt.a28ef053.png" width="36%" alt="ChatGPT 桌面版里的 Codex 铺满 Snug 手机屏"> <img src="https://dl.zephyrxiang.com/snug/media/phone-fit-settings.4e526ad6.png" width="36%" alt="限定了最小宽度的系统设置完整铺满手机屏"><br><sub>左：ChatGPT 桌面版里的 Codex（文件链接已打码）· 右：限定了最小宽度的系统设置，仍完整铺满</sub></p>

### 任意手机都按比例铺满

UU远程 将画面等比缩放后放进手机，比例不符时会留下黑边。Snug 按手机型号计算手机屏的比例，使画面恰好铺满。

- **不分系统与品牌**：iPhone、iPad 与各品牌安卓设备全面支持，内置 17 个品牌的 800 款机型
- **自定义**：列表中没有的机型，直接填写屏幕分辨率或比例

<p align="center"><img src="https://dl.zephyrxiang.com/snug/media/phone-models.3137ee0e.png" width="36%" alt="手机型号页：当前机型、自定义分辨率与按品牌列出的机型"><br><sub>手机型号：按品牌列出机型，也可自定义分辨率</sub></p>

## 🆚 与其他方案的对比

| 方案 | 局限 | Snug |
| :--- | :--- | :--- |
| **AI 助手的远程控制** | 只能操作自家 AI 的会话 | 任意终端与 App 均可使用，见 [任意 App](#任意-app-都能使用完整铺满) |
| **UU远程 自带终端** | 独立于本机终端，断开后进程仍驻留 | 直接使用 Mac 原有的终端窗口 |

- **AI 助手的远程控制**：如 Claude Remote Control、ChatGPT 中的 Codex，均与单一产品绑定
- **UU远程 自带终端**：运行在 UU远程 自己的 tmux 中，电脑上进行中的工作（如终端里的 Claude Code）无法在其中接续

## 📦 安装

> **系统要求**：macOS 14 或更新版本，被远程的 Mac 已安装 [UU远程](https://uuyc.163.com/)。手机端只需 UU远程。

**下载安装（推荐）**

1. 从「[下载](#-下载)」一节获取安装包（中国大陆推荐国内源）
2. 将 Snug 拖入「应用程序」并打开
3. 按欢迎页提示授予辅助功能权限（macOS 27 起名为「设备控制和数据访问」）

完成后 Snug 常驻菜单栏，此后无需任何操作。需要查看状态时，再次打开 Snug 即弹出设置。

**更新**：启动时自动检查，有新版本时一键更新，设置与授权保留；也可在「关于」页手动检查或切换更新源。2.1.0 之前的版本请手动下载替换。

<div align="center">
<img src="https://dl.zephyrxiang.com/snug/media/welcome.529d7d28.png" width="30%" alt="欢迎页"> <img src="https://dl.zephyrxiang.com/snug/media/settings-ready.ee67ab30.png" width="30%" alt="设置"> <img src="https://dl.zephyrxiang.com/snug/media/about.5534d9dc.png" width="30%" alt="关于：版本、软件更新与更新源">
<p><sub>欢迎页 · 设置 · 关于</sub></p>
</div>

**从源码构建**（需要 Xcode 与 Swift 6）

```bash
git clone https://github.com/zephyr4123/snug.git
cd snug && scripts/snug install
```

<details>
<summary>从「UU远程伴侣」迁移</summary>

两者同时运行会相互干扰，Snug 启动时会提示退出旧版。若在旧版中启用过守护助手，请再执行：

```bash
launchctl bootout gui/$(id -u)/com.corndog.uudisplay.watchdog-agent
rm ~/Library/LaunchAgents/com.corndog.uudisplay.watchdog-agent.plist
```

</details>

## 🧭 使用方式

1. **切换屏幕**：手机或平板连接后，在 UU远程 的屏幕列表中选择「Snug 手机屏」

   <img src="https://dl.zephyrxiang.com/snug/media/uu-screen-picker.212fdbe9.png" width="50%" alt="UU远程 的屏幕列表：显示屏 3（Snug 手机屏）">

2. **选择手机型号**（仅首次）：在 App 资源库右上角或 Snug 设置中选择。选好后在 UU远程 中切到其他屏幕再切回，手机屏即按新的比例铺满
3. **选择 App 或窗口**：在 App 资源库中轻点 App 或窗口，即铺满整屏；未运行的 App 直接打开
4. **切换窗口**：轻点底部指示条返回 App 资源库；另选窗口时，原窗口自动归位
5. **结束远程**：窗口回到原位、字号复原，手机屏随之移除

## 🔒 隔离与还原

- **只改动手机屏**：不更改主显示器，不关闭任何屏幕，不触碰其他窗口；不切换至手机屏时，UU远程 与原版完全一致
- **先记录，后改动**：窗口的原始位置与字号先行记录，Snug 意外退出后也能还原
- **窗口被直接关掉也能还原**：在手机上直接关掉的窗口，App 下次带着手机屏上的大小打开时，自动放回原位、缩回字号
- **无响应时保持原状**：App 暂时无响应时不强行移动窗口，并在屏上说明原因
- **不收集数据**：除检查更新外不连接任何服务器，不收集任何数据

## 🚧 已知限制

| 限制 | 说明 |
| :--- | :--- |
| 依赖 macOS 未公开接口 | 系统大版本更新后可能失效 |
| 仅提供竖屏 | 平板横向使用时为 UU远程 原版体验；平板横屏在后续计划中 |
| 非网易官方软件 | 与 UU远程 及其开发商无关；UU远程 更新后部分功能可能受影响 |
| 远程结束后手机屏偶尔未移除 | 推流停止未被系统日志记录时出现，菜单栏仍显示「远程中」；在设置中关闭手机屏即可还原 |
| VS Code、Cursor 窗口在手机上用快捷键或菜单关闭后，字号可能被记住 | 远程结束后重新打开时若字号偏大，按 ⌘- 复原（关闭当时手机屏上会提示级数）；开着手势时轻点窗口的关闭按钮，或在 App 资源库中关闭，则不受影响 |
| 未接显示器的 Mac 上切不回原来的屏幕 | macOS 只在没有任何显示器时提供默认屏幕，手机屏出现后即被撤下；在 App 资源库右上角退出手机屏即可回到原来的画面 |

各项限制的成因见 [工作原理](docs/how-it-works.md#已知限制的技术原因)。

## 🔧 开发

```bash
scripts/snug check     # 构建并运行全部测试
scripts/snug install   # 构建、安装并打开
```

- 代码分为三层：`SnugCore`（纯逻辑）→ `SnugSystem`（系统接口）→ `Snug`（界面）
- 打包、发布、启动参数与数据位置：[开发文档](docs/development.md)
- 连接判定、窗口移动与手势翻译：[工作原理](docs/how-it-works.md)

## 🧹 卸载

1. 在菜单栏中退出 Snug（窗口会先还原）
2. 将「应用程序」中的 Snug 移到废纸篓
3. 可选：删除设置与日志 `rm -rf ~/Library/Application\ Support/Snug ~/Library/Logs/Snug.log*`
4. 可选：在系统设置的「辅助功能」中移除 Snug

## 🙏 致谢

首先，谨向 **[UU远程](https://uuyc.163.com/)** 致以诚挚的敬意。

- **品质卓越**：连接稳定，响应灵敏，画质与帧率均可按需调节。远程控制涉及编码、传输与跨网络连接等诸多工程环节，能够达到日常工作可长期依赖的水准，其技术门槛不言而喻。
- **完全免费**：技术壁垒如此之高的产品，始终向所有用户免费开放，其意义远非「难得」二字所能概括，堪称对整个远程生态的慷慨馈赠。正是这份开放，使社区得以在其基础上持续探索，远程伴侣、单指滚动、虚拟屏幕等方案相继涌现，Snug 亦由此而生。

谨此致谢。

亦感谢以下项目与贡献者：

- [UUyc-companion](https://github.com/aoyamashou/UUyc-companion)（MIT）：AoyamaShou，Snug 的前身
- [Better Remote](https://github.com/pincc-debug/better-remote)（MIT）：pincc-debug，为 UU远程 实现单指滚动的思路
- [PermissionFlow](https://github.com/jaywcjlove/PermissionFlow)（MIT）：小弟调调，授权引导与拖拽授权面板
- [FreeDisplay](https://github.com/huberdf/FreeDisplay)（MIT）：huberdf，`CGVirtualDisplay` 接口声明
- 社区 PR 作者：bubble、gencheng liu、Rongzheng Liu（#10，全屏桌面空间下读取 UU远程 窗口）
- 虚拟屏的实现参考了 [DeskPad](https://github.com/Stengo/DeskPad)、[Side Screen](https://github.com/tranvuongquocdat/SideScreen) 与 Chromium 的相关代码

## ⭐ Star 趋势

<p align="center"><a href="https://www.star-history.com/#zephyr4123/snug&Date"><picture><source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=zephyr4123/snug&type=Date&theme=dark"><source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=zephyr4123/snug&type=Date"><img src="https://api.star-history.com/svg?repos=zephyr4123/snug&type=Date" width="80%" alt="Snug 的 GitHub Star 数随时间的变化"></picture></a></p>

## 📄 许可证

[MIT](LICENSE) © 2026 AoyamaShou，© 2026 zephyr4123
