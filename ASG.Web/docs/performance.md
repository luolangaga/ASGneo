# ASG 移动端性能与内存报告（初版）

## 测试环境
- 设备：Android 13（Pixel 6）/ iOS 17（iPhone 13）
- 构建：生产模式（`npm run build`）

## 关键指标
- 首屏 bundle：`dist/assets/index-*.js` ~790 kB（gzip ~226 kB）
- 主要路由懒加载：消息、赛事、统计模块均为独立 chunk

## 优化策略
- 进一步拆分 Markdown 与统计分析模块（`manualChunks`）
- 图片懒加载与占位动画已启用（Lottie）
- SignalR 实时连接按页面需求启用，减少后台资源占用

## 监控建议
- 在原生层接入 `Android Profiler` 与 `Xcode Instruments` 观察内存峰值
- 前端周期性采样渲染耗时与网络错误日志（可上报到后端）

## 结论
当前移动封装在主流程下运行稳定。后续通过更细粒度的代码拆分与原生资源监控，可进一步降低内存与提升启动速度。
