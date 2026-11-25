# CSharp Passkey Manager（C# / WinUI 3）

[English README](README.en.md)

参考微软 [Windows-classic-samples/PasskeyManager](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/PasskeyManager) 的纯 C# 插件式 Passkey Manager。它调用真实 Windows WebAuthn Plugin API，不是模拟界面。

> 仅用于官方插件流程的学习与测试，不是生产级密码库。创建、登录和删除操作会生成、使用或永久删除真实 CNG 密钥。

## 已实现

- MSIX `com:ExeServer` + `-PluginActivated` 后台 COM 激活。
- 托管 `IClassFactory` / `IPluginAuthenticator`：
  - `MakeCredential`
  - `GetAssertion`
  - `CancelOperation`
  - `GetLockStatus`
- `WebAuthNPluginAddAuthenticator` / `UpdateAuthenticatorDetails` / `RemoveAuthenticator` / `GetAuthenticatorState`。
- 使用 Windows 官方 `WebAuthNDecode*` 解析请求、`WebAuthNEncode*` 编码响应。
- 强制验证平台请求签名与 `WebAuthNPluginPerformUserVerification` 的 UV 响应签名；失败即中止。
- Windows Hello 插件用户验证，以及锁定保险库时的真实 Hello 解锁。
- Windows CNG 持久化 ECDSA P-256 私钥、COSE ES256 公钥、authenticatorData 与 DER 签名。
- 本地真实凭据元数据、按 RP ID/allowList 匹配、系统自动填充缓存增删查。
- 单操作互斥、事务 ID 取消、精简 UI 及“锁定/多候选必须显示 UI”规则。
- 搜索、缓存切换、永久删除单个或全部凭据、操作日志。
- 中英文界面资源，按系统语言自动选择。
- 真实网站生产使用警告：非学习白名单 RP 会强制显示红色加粗警告和确认窗口，精简 UI 不会将其跳过。

## 平台要求

与微软官方示例一致：

- Windows 11 24H2：build 26100.6725 或更高；或 Windows 11 25H2：build 26200.6725 或更高。
- Windows SDK 10.0.26100.7175 或更高。
- 支持本项目 `net11.0-windows10.0.26100.0` 的 .NET SDK。
- WinApp CLI 0.7+；Windows 开发者模式。
- 已配置 Windows Hello PIN 或生物识别。

较旧系统可能可以显示管理 UI，但真实插件 API 不受支持。

## 构建与启动

```powershell
dotnet restore
dotnet build
winapp run . --detach --json
```

必须以打包方式运行，使 MSIX COM 激活与 WebAuthn 插件身份生效。不要直接运行 `bin` 中的 EXE 来测试插件。

## 真实测试步骤

1. 启动应用，进入“设置”，打开“注册插件”。此操作调用 `WebAuthNPluginAddAuthenticator`。
2. 点击“打开 Windows 设置”，进入“账户 > 通行密钥 > 高级选项”，手动启用 **CSharp Passkey Manager**。
   - Windows 不允许应用代替用户静默启用提供程序。
3. 从概览打开 [webauthn.io](https://webauthn.io/)，输入测试用户名并注册。
4. 在系统提供程序列表中选择 **CSharp Passkey Manager**。
5. 在插件窗口确认保存，完成 Windows Hello 验证。
6. 返回主界面：凭据列表应显示真实 RP、用户与凭据 ID，并已加入 Windows 自动填充缓存。
7. 在 webauthn.io 使用同一账户登录；选择凭据并再次完成 Hello 验证，网站应接受真实断言。
8. 测试取消：关闭插件窗口或点击取消，网站应收到用户取消，且不会创建新凭据。

如需清理，先在应用内删除测试凭据，再关闭“注册插件”。删除凭据不可撤销。

## 本地化与生产警告

- `Strings/en-US/Resources.resw` 和 `Strings/zh-CN/Resources.resw` 覆盖 XAML、ViewModel、对话框、操作日志及插件确认窗口；默认回退语言为英文。
- 设置页提供“中文（简体）/ English”选择器，通过 Windows App SDK `PrimaryLanguageOverride` 保存偏好并立即刷新所有页面。
- `Services/RelyingPartyRules.cs` 明确区分学习站点和生产 RP。学习白名单包括 webauthn.io、webauthn.me、webauthn.guide、passkeys.dev、localhost、回环地址及保留测试域名。
- 生产 RP 始终显示大号红色加粗警告，要求用户明确确认。不要将该警告理解为生产适用性的许可；本软件仍只能用于学习。

## 数据与安全边界

- 公钥元数据：打包应用的 `LocalState\CredentialsDB\credentials.json`。
- 私钥：Microsoft Software KSP 中的持久化 CNG ECDSA P-256 密钥。每个凭据使用 `csharp-passkeys/` 前缀加独立随机名称，同一用户多次注册不会覆盖旧密钥。
- 设置：`HKCU\Software\CSharpPasskeyManager` 中的 `VaultLocked` 和 `SilentOperation`。
- 日志位于打包应用 `LocalState\plugin.log`；不记录私钥、UV 响应、签名、凭据 ID 或完整请求缓冲区。
- 为与官方示例保持一致，签名计数固定为 0，authenticatorData flags 使用 `0x5d` / `0x1d`。生产实现应持久化计数器并准确声明备份状态。
- 未实现云同步、企业证明、PRF 保险库、完整 FIDO 扩展或生产级恢复机制。

## 代码结构

```text
Program.cs                       STA、COM 安全与 WinUI 启动
App.xaml.cs                      交互模式 / -PluginActivated 后台模式
Interop/
  IPluginAuthenticator.cs        COM IID 与 vtable 契约
  PluginConstants.cs             CLSID、AAGUID、插件 RP ID
  WebAuthnTypes.cs               官方结构体的 x64 C# 布局
  WebAuthnNative.cs              webauthn.dll P/Invoke
Services/
  PluginComServer.cs             托管 IClassFactory / CoRegisterClassObject
  PluginAuthenticator.cs         MakeCredential / GetAssertion / UV / Cancel
  WebAuthnCrypto.cs              CNG、COSE、authenticatorData、签名与验签
  PluginRegistrationService.cs   插件注册、更新、状态与移除
  CredentialStore.cs             真实本地公钥元数据
  SystemCredentialCache.cs       Windows 自动填充凭据缓存
  PluginOperationCoordinator.cs  单操作 UI 与取消编排
  WindowsHelloService.cs         保险库 Hello 解锁
  AppStrings.cs                  ResourceLoader 字符串加载
  LanguageService.cs             语言偏好与立即刷新
  RelyingPartyRules.cs           学习站点白名单与生产警告规则
Strings/
  en-US/Resources.resw           英文界面资源
  zh-CN/Resources.resw           中文界面资源
Views/
  MainWindow.xaml(.cs)           Mica 主窗口、尺寸与页面宿主
  MainPage.xaml(.cs)             导航壳、保险库与全局状态栏
  OverviewPage.xaml(.cs)         概览、统计与测试入口
  PasskeysPage.xaml(.cs)         搜索、凭据详情、缓存与删除
  ActivityPage.xaml(.cs)         操作日志
  SettingsPage.xaml(.cs)         插件设置、语言切换与清理
  PluginOperationWindow.cs       创建确认与登录凭据选择窗口
ViewModels/
  MainPageViewModel.cs           管理界面的真实状态与异步操作
Models/
  PasskeyCredential.cs           列表/详情的显示模型
  StoredCredential.cs            可持久化的真实公钥元数据
  ActivityEntry.cs               操作日志显示模型
  StatusSeverity.cs              UI 无关的状态级别
```

## MVVM 约定

- `Models` 只保存凭据与日志数据及显示状态，不调用 WebAuthn API。
- `ViewModels` 暴露可绑定属性与异步操作，不直接创建窗口或对话框；`MainPageViewModel.Shared` 是各独立页面共享的唯一状态实例。
- `Views` 只负责 XAML、导航、用户确认和窗口生命周期。
- `Services` 负责原生插件、CNG、Hello、存储与跨进程状态；`Interop` 只定义官方 ABI 契约。

## 本机验证记录

在本项目目标 Windows 11 环境中已实际完成：

- `WebAuthNPluginAddAuthenticator` 注册成功，并由用户在系统设置中启用。
- webauthn.io 真实 MakeCredential：C# COM 后台激活、平台请求验签、Windows Hello PIN、UV 响应验签、CNG ES256 密钥与官方响应编码均通过。
- webauthn.io 真实 GetAssertion：凭据匹配、Hello UV、CNG 签名与网站登录均通过。
- 用户取消返回 `NTE_USER_CANCELLED (0x80090036)`。
- 本地元数据与 Windows 自动填充缓存均显示保留的测试凭据。
- 中英文双向即时切换通过；四个独立页面的导航、资源键和共享 ViewModel 状态正常。
- 生产 RP 判定测试 10/10，通过真实网站、测试站点、回环地址和伪装域名用例。
- `dotnet build` 与 `dotnet build -c Release`（x64）均为 0 警告、0 错误。

诊断日志位于打包应用 `LocalState\plugin.log`，可用于区分请求验签、Hello UV、CNG 与响应编码阶段。

## 验证边界

`dotnet build` 可验证 C#、XAML、COM 声明与互操作布局。真实注册会改变当前用户的 Windows 提供程序列表；启用和 Hello 验证必须由用户亲自完成。网站的端到端结果还依赖浏览器、RP 配置与用户手势，不能由静态构建替代。

## 许可证

MIT，详见 `LICENSE`。微软示例、头文件与模板资源的归属说明见 `NOTICE.md`。
