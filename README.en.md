# CSharp Passkey Manager (C# / WinUI 3)

[中文说明](README.md)

A pure C# plugin passkey manager based on Microsoft's [Windows-classic-samples/PasskeyManager](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/PasskeyManager). It calls the real Windows WebAuthn Plugin APIs rather than simulating the workflow.

> **For learning and evaluation only. Do not use this project with production accounts or as a production credential vault.** Creating, using, and deleting credentials performs real operations on persistent CNG keys.

## Features

- MSIX `com:ExeServer` and `-PluginActivated` background COM activation.
- Managed `IClassFactory` and `IPluginAuthenticator` implementations:
  - `MakeCredential`
  - `GetAssertion`
  - `CancelOperation`
  - `GetLockStatus`
- `WebAuthNPluginAddAuthenticator`, `UpdateAuthenticatorDetails`, `RemoveAuthenticator`, and `GetAuthenticatorState`.
- Official Windows `WebAuthNDecode*` request decoders and `WebAuthNEncode*` response encoders.
- Mandatory verification of platform request signatures and the signature returned by `WebAuthNPluginPerformUserVerification`; verification failures abort the operation.
- Real Windows Hello user verification and vault unlocking.
- Persistent ECDSA P-256 private keys in Windows CNG, COSE ES256 public keys, authenticator data, and DER signatures.
- Real local credential metadata, RP ID/allowList matching, and Windows autofill cache management.
- Single-operation exclusion, transaction-aware cancellation, minimized UI, and mandatory UI for locked vaults or multiple candidates.
- Search, cache toggles, permanent deletion, and operation logs.
- English and Simplified Chinese UI resources selected automatically from the system language, with English as the fallback.
- A prominent red, bold production-use warning for non-learning relying parties. Production RP IDs always show the warning and confirmation window, even when minimized UI is enabled.

## Requirements

Aligned with Microsoft's official sample:

- Windows 11 24H2 build 26100.6725 or later, or Windows 11 25H2 build 26200.6725 or later.
- Windows SDK 10.0.26100.7175 or later.
- A .NET SDK supporting `net11.0-windows10.0.26100.0`.
- WinApp CLI 0.7 or later and Windows Developer Mode.
- Windows Hello PIN or biometric verification configured.

Older systems may display the management UI, but the real plugin APIs are not supported there.

## Build and run

```powershell
dotnet restore
dotnet build
winapp run . --detach --json
```

Run the app with package identity so MSIX COM activation and the WebAuthn plugin identity are available. Do not test plugin activation by launching the EXE directly from `bin`.

## Test with a real WebAuthn flow

1. Start the app, open **Settings**, and enable **Register plugin**. This calls `WebAuthNPluginAddAuthenticator`.
2. Select **Open Windows Settings**, navigate to **Accounts > Passkeys > Advanced options**, and manually enable **CSharp Passkey Manager**.
   - Windows intentionally prevents an app from silently enabling a provider on the user's behalf.
3. Open [webauthn.io](https://webauthn.io/) from the overview, enter a test username, and register.
4. Select **CSharp Passkey Manager** in the Windows provider picker.
5. Confirm the operation in the plugin window and complete Windows Hello verification.
6. Return to the management UI. The list should show the real RP, username, and credential ID, with the credential added to the Windows autofill cache.
7. Authenticate with the same account on webauthn.io. Select the credential and complete Hello verification again; the website should accept the real assertion.
8. Test cancellation by closing the plugin window or selecting Cancel. The website should receive a cancellation and no new credential should be created.

To clean up, delete the test credentials in the app before unregistering the plugin. Credential deletion is irreversible.

## Localization and production warnings

- `Strings/en-US/Resources.resw` and `Strings/zh-CN/Resources.resw` cover XAML, view models, dialogs, logs, and the plugin operation window.
- The Settings page provides a **中文（简体）/ English** selector. It uses Windows App SDK `PrimaryLanguageOverride` to persist the preference and immediately rebuilds the navigation pages.
- `Services/AppStrings.cs` loads strings through Windows App SDK `ResourceLoader` on both UI and COM/RPC threads.
- `Services/RelyingPartyRules.cs` defines the learning-site allowlist: webauthn.io, webauthn.me, webauthn.guide, passkeys.dev, localhost, loopback addresses, and reserved testing domains.
- Every other RP is treated as production. Its operation window displays a large, bold red warning and cannot be suppressed by minimized UI. This warning is a safety stop for evaluation, not a statement that the software is suitable for production.

## Data and security boundaries

- Public metadata: `LocalState\CredentialsDB\credentials.json` in the packaged application's data folder.
- Private keys: persistent ECDSA P-256 keys in the Microsoft Software KSP. Every credential has an independent random key name prefixed with `csharp-passkeys/`, so repeated registration for one user does not overwrite older keys.
- Settings: `VaultLocked` and `SilentOperation` under `HKCU\Software\CSharpPasskeyManager`.
- Logs: `LocalState\plugin.log`. Private keys, UV responses, signatures, credential IDs, and complete WebAuthn request/response buffers are not logged.
- To match the official sample, the signature counter remains zero and authenticator-data flags are `0x5d` / `0x1d`. A production implementation must persist counters and report backup state accurately.
- Cloud synchronization, enterprise attestation, a PRF-protected vault, complete FIDO extensions, and production-grade recovery are not implemented.

## Project layout

```text
Program.cs                       STA, COM security, and WinUI startup
App.xaml.cs                      Interactive and -PluginActivated modes
Interop/
  IPluginAuthenticator.cs        COM IID and vtable contract
  PluginConstants.cs             CLSID, AAGUID, and plugin RP ID
  WebAuthnTypes.cs               Official structures with x64 C# layout
  WebAuthnNative.cs              webauthn.dll P/Invoke declarations
Services/
  PluginComServer.cs             Managed IClassFactory / CoRegisterClassObject
  PluginAuthenticator.cs         MakeCredential / GetAssertion / UV / Cancel
  WebAuthnCrypto.cs              CNG, COSE, authenticator data, signing, verification
  PluginRegistrationService.cs   Registration, update, state, and removal
  CredentialStore.cs             Real local public credential metadata
  SystemCredentialCache.cs       Windows autofill credential cache
  PluginOperationCoordinator.cs  Single-operation UI and cancellation flow
  WindowsHelloService.cs         Windows Hello vault unlock
  AppStrings.cs                  ResourceLoader string access
  LanguageService.cs             Language preference and immediate UI refresh
  RelyingPartyRules.cs           Learning allowlist and production-warning rules
Views/
  MainWindow.xaml(.cs)           Mica window, sizing, and page host
  MainPage.xaml(.cs)             Navigation shell, vault, and global status
  OverviewPage.xaml(.cs)         Counts, recent activity, and test entry points
  PasskeysPage.xaml(.cs)         Search, credential details, cache, and deletion
  ActivityPage.xaml(.cs)         Native operation log
  SettingsPage.xaml(.cs)         Plugin settings, language selection, and cleanup
  PluginOperationWindow.cs       Creation and sign-in confirmation window
ViewModels/
  MainPageViewModel.cs           Real management state and async actions
Models/
  PasskeyCredential.cs           List/detail presentation model
  StoredCredential.cs            Persistable public credential metadata
  ActivityEntry.cs               Activity presentation model
  StatusSeverity.cs              UI-independent status severity
Strings/
  en-US/Resources.resw           English UI resources
  zh-CN/Resources.resw           Simplified Chinese UI resources
```

## MVVM conventions

- `Models` contains credential/log data and presentation state, with no WebAuthn API calls.
- `ViewModels` exposes bindable properties and asynchronous actions without creating windows or dialogs. `MainPageViewModel.Shared` is the single state owner shared by all independent pages.
- `Views` owns XAML, navigation, confirmation UI, and window lifetimes.
- `Services` owns native plugins, CNG, Hello, persistence, and cross-process state; `Interop` contains only official ABI contracts.

## Verification performed locally

On the target Windows 11 environment, the following was completed successfully:

- Plugin registration through `WebAuthNPluginAddAuthenticator`, followed by manual enablement in Windows Settings.
- Real webauthn.io MakeCredential: C# COM activation, platform request verification, Windows Hello PIN, UV response verification, CNG ES256 key creation, and official response encoding.
- Real webauthn.io GetAssertion: credential matching, Hello UV, CNG signing, and website authentication.
- User cancellation returned `NTE_USER_CANCELLED (0x80090036)`.
- The retained test credential appeared in both local metadata and the Windows autofill cache.
- Immediate English/Chinese switching passed in both directions; all four independent pages retained working navigation, resources, and shared view-model state.
- Relying-party classification passed 10/10 checks covering production sites, learning sites, loopback addresses, and deceptive domain suffixes.
- `dotnet build` and `dotnet build -c Release` (x64) both completed with zero warnings and zero errors.

Diagnostic logs in `LocalState\plugin.log` distinguish request verification, Hello UV, CNG, and response-encoding stages.

## Verification limits

`dotnet build` validates C#, XAML, COM declarations, and interop layout. Real registration changes the current user's provider list, while enablement and Hello verification require user interaction. End-to-end website results also depend on the browser, RP configuration, and user gestures and cannot be established by a static build alone.

## License

MIT; see `LICENSE`. Attribution for Microsoft samples, headers, and template assets is listed in `NOTICE.md`.
