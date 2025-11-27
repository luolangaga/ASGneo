# ASG 移动端应用部署与发布指南

## 概述
本应用基于现有 `ASG.Web` 前端，使用 Capacitor 封装为原生 iOS/Android 应用。集成了系统消息的实时推送桥接、本地通知与通知点击跳转。

## 环境准备
- Node.js 18+
- Android Studio（含 SDK 与构建工具）
- Xcode 15+（macOS）
- 已配置的 FCM（Android）与 APNs（iOS）证书与密钥

## 安装依赖
```bash
npm install
```

## 构建 Web 资源
```bash
npm run build
```

## 添加平台并同步
```bash
npm run cap:add:android
npm run cap:add:ios
npm run cap:sync
```

## 打开原生工程
```bash
npm run cap:open:android
npm run cap:open:ios
```

## 推送配置
### Android (FCM)
1. 在 Firebase 控制台创建项目，启用 Cloud Messaging。
2. 下载 `google-services.json` 放置到 `android/app/`。
3. 在 Android 工程的 `build.gradle` 中启用 Google Services 插件（Capacitor 插件模板会引导）。
4. 在应用启动后将设备 Token 上报到后端接口以实现远程推送。

### iOS (APNs)
1. 在 Apple 开发者后台创建 APNs Key 并下载 `.p8` 文件。
2. 在 Xcode 中打开项目，启用 `Push Notifications` 与 `Background Modes`（Remote notifications）。
3. 配置 `Bundle Identifier` 与签名。
4. 在应用启动后将设备 Token 上报到后端接口以实现远程推送。

## 通知点击跳转
移动端在 `src/mobile/push.js` 中监听 `pushNotificationActionPerformed` 与本地通知的 `localNotificationActionPerformed`，通过 `extra.route` 实现页面跳转。

## 后端对接建议
后端已具备 SignalR 实时消息能力（`/hubs/app`）。为了在后台与离线状态下实现推送：
- 增加设备 Token 存储接口（示例：`POST /api/Devices/register { token, platform }`）。
- 在产生消息与系统通知时，调用 FCM/APNs 发送远程推送，payload 中包含 `route` 字段。

## 构建与发布
### Android
- 使用 Android Studio 生成签名的 `app-release.apk` 或 `app-release.aab`。
- 配置 `targetSdkVersion` 与 `minSdkVersion`，移除未使用权限。
### iOS
- 使用 Xcode 归档并通过 `Organizer` 提交到 App Store Connect。
- 配置应用图标与启动图；填写隐私与推送用途说明。

## 应用商店规范
- 明确隐私政策与数据收集用途。
- 说明推送通知使用场景（消息提醒）。
- 避免持续后台运行与过度唤醒，遵循系统限制（iOS 需使用 Remote notifications）。

## 常见问题
- Android 无法接收推送：检查 `google-services.json` 与包名一致性。
- iOS 无法接收推送：检查 APNs Key、Team ID 与签名配置，确保启用 `Background Modes`。
