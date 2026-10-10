# 哔哩小窗

<img src="mini.png" alt="哔哩小窗图标" width="80" />

陈小铭开发的 Windows 副屏迷你浏览器。作者主页：https://space.bilibili.com/2121431899

适合在游戏间隙用副屏看 B 站视频，采用深色圆角窗口，支持多标签、置顶和后台休眠。

## 下载与运行

前往 [Releases 下载 Windows x64 便携版](https://github.com/cxm133777/BiliMiniBrowser/releases/latest)，解压整个压缩包后运行 `BiliMini.exe`。请保留同目录的 DLL、XAML、JS 和图标文件，不要只移动 EXE。

需要 64 位 Windows、.NET Framework 4.8 和 Microsoft Edge WebView2 Runtime。运行包不包含 WebView2 Runtime；内核启动失败时，请检查是否已安装该运行时。

当前版本为未做数字签名的个人开源软件。建议从本仓库 Releases 下载，也可按下方说明自行编译。

双击 `BiliMini.exe` 或桌面「哔哩小窗」快捷方式即可运行。首次打开优先放在副屏，窗口位置和大小会自动保存。每次启动从 B 站首页开始，不携带开发者个人浏览记录。

- 地址栏只读，显示当前 B 站网址；点击「复制链接」或选中后 Ctrl+C 复制。搜索使用 B 站网页内的搜索框。应用只允许 bilibili.com 及其子域的页面跳转，外站和外部客户端链接会被拦截。
- 顶部「+」或 Ctrl+T 新建 B 站标签页，点击标签切换，点击「×」或 Ctrl+W 关闭。B 站网页要求新窗口时会打开新标签页。标签栏可用滚轮横向滚动，页面及进度在标签切换时保留；各标签共享登录状态。
- 图钉按钮控制置顶，显示器按钮将窗口移动到下一块屏幕。
- 观影按钮将 B 站播放器铺满网页区域；再点一次恢复。
- Ctrl+Alt+B 显示或隐藏窗口。默认隐藏或最小化时暂停视频并尝试休眠网页，恢复窗口时继续播放；可在更多菜单关闭此行为。
- 红色圆点、Alt+F4、更多菜单和托盘菜单的退出都会彻底关闭程序，释放各标签页与网页内核。临时隐藏请使用 Ctrl+Alt+B。
- 默认后台标签页暂停视频并尝试休眠，减少后台脚本、动画和解码开销，切回时保留页面和播放进度。手动暂停的视频不会自动播放。需要后台听视频时，可关闭更多菜单中的「后台标签页自动休眠」。
- 更多菜单提供窗口尺寸、网页缩放、动态、个人空间和关于信息。
- 「我的空间 / 收藏」使用 `https://space.bilibili.com/` 通用入口，不预设任何账号 ID；请先登录自己的 B 站账号。「动态」使用 `https://t.bilibili.com/`。
- B 站账号直接在官方网页登录，登录状态保存在本机独立浏览器目录中，与其他浏览器分别管理。

窗口保持在自身范围内，网页全屏不会占满整块显示器。置顶适用于桌面和一般无边框游戏窗口；独占全屏游戏可能覆盖普通桌面窗口。

网页内核保留默认硬件加速，不强制改动视频画质、帧率或弹幕设置。正在播放的视频开销还取决于网站、编码、分辨率与设备；本次优化主要减少未观看标签页及隐藏窗口的开销。休眠接口是尽力执行，特殊页面可能无法进入休眠，此时仍会暂停媒体。实现参考 [Microsoft WebView2 性能指南](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)。

## 数据与隐私

设置、Cookie 和缓存位于 `%LOCALAPPDATA%\BiliMiniBrowser`。应用没有自建服务器，不主动上传账户资料；访问网站产生的请求由相应网站处理。退出后删除该目录可清除登录和设置。

## 声明与授权

本软件为陈小铭独立开发的第三方浏览工具，与哔哩哔哩、Apple、Microsoft 无隶属或官方合作关系。视频、商标及页面内容归各权利人所有。登录、清晰度、会员与播放权限由网站决定，软件不提供下载或绕过权限功能。

原创源码采用 MIT 许可证，详见 LICENSE。第三方浏览器组件遵循各自条款，详见 THIRD_PARTY_NOTICES.md 和 WebView2-LICENSE.txt。软件按现状提供，不对网站改版或服务可用性作保证。

## 构建

依赖 Windows .NET Framework 4.8 和 Microsoft Edge WebView2 Runtime。克隆或下载本仓库后，在源码目录运行以下命令。脚本首次构建会从 Microsoft 官方 NuGet 源还原固定版本的 WebView2 SDK，需要联网。使用内置框架编译器，无需安装完整开发环境：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

构建输出为当前目录的 `BiliMini.exe`。内置检查可用 `BiliMini.exe --self-test` 运行，结果写入 `self-test.json`；检查使用独立测试资料目录，不使用正常浏览的登录资料。

WebView2 开发文档：https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
