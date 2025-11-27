## 项目目标
- 新建前端项目 `ASG.Person`，支持解说/导播/裁判的招募、申请、审核、赛程同步与工资结算。
- 面向两类用户：主办方（发布任务、审核、结算）与人员用户（注册、申请、查看安排与工资）。
- 与现有后端 `ASG.Api` 对接，复用赛程与事件数据；同意后将人员信息同步到赛程的 `commentator/director/referee` 字段并触发邮件通知。

## 技术栈与结构
- 框架：Vue 3 + Vite，与 `ASG.Web`/`ASG.Admin` 保持一致。
- UI：Vuetify（复用 `ASG.Web/src/plugins/vuetify.js` 的主题与默认值）。
- 路由：Vue Router。
- 状态：轻量式 `stores/auth.js` 与通知 `stores/notify.js`，沿用现有模式。
- 服务层：`src/services/api.js` 封装统一的 `apiFetch`，与现有项目一致；新增模块：`recruitments.js`、`applications.js`、`payroll.js`。
- 环境变量：`VITE_API_BASE_URL` 指向 `ASG.Api`。
- 目录：`ASG.Person/{src/components, src/views, src/services, src/stores, src/router, src/plugins}`。

## 核心功能
- 招募任务：主办方创建任务（所属赛事/赛程、职位类型、每场单价、名额、要求与说明）。
- 人员注册与资料：用户注册后选择身份（解说/导播/裁判），维护可用时间与简介。
- 申请与审核：人员查看任务并提交申请；主办方审核同意或拒绝。
- 赛程同步：同意后将人员信息同步到相应 `Match` 的 `Commentator/Director/Referee` 字段；支持单场或多场批量同步。
- 通知邮件：同意后发送邮件给报名者（后端通过已有 `EmailService` 实现）。
- 工资结算：按“每场单价 × 已安排场次”统计；支持筛选时间范围、导出 CSV。

## 页面与路由
- `RecruitmentBoardView`：招募任务列表（按赛事、职位筛选、搜索）。
- `RecruitmentDetailView`：任务详情（展示赛程、要求、工资标准），人员可申请。
- `ApplyView`：申请表（备注、可用时段）。
- `MyApplicationsView`：我的申请与状态（Pending/Approved/Rejected）。
- `MyAssignmentsView`：已安排的比赛与职位，快捷导航至赛程详情。
- `PayrollView`：工资结算统计与导出。
- `OrganizerDashboardView`：主办方任务管理（创建/编辑、审核申请、批量同意并同步赛程）。
- `EventSchedulePicker` 组件：从赛事赛程选择场次，复用时间轴视觉（参考 `ASG.Web/src/views/EventScheduleView.vue`）。
- 认证相关视图可复用 `LoginView`/`RegisterView` 的交互样式。

## 前端数据模型（临时）
- PositionType：`Commentator | Director | Referee`。
- RecruitmentTask：`{ id, eventId, matchIds[], positionType, payPerMatch, slots, description, requirements, status }`。
- Application：`{ id, taskId, applicantUserId, note, status, createdAt }`。
- Assignment：`{ matchId, positionType, userId, payPerMatch }`（用于结算与展示）。

## 接口对接（与后端协作）
- 招募任务：`GET /api/recruitments?eventId=...`、`POST /api/recruitments`、`PUT /api/recruitments/{id}`、`DELETE /api/recruitments/{id}`。
- 申请：`POST /api/recruitments/{id}/apply`、`GET /api/recruitments/{id}/applications`、`POST /api/applications/{id}/approve`、`POST /api/applications/{id}/reject`。
- 同步赛程：`POST /api/applications/{id}/sync-matches`（将人员写入 `Match.Commentator/Director/Referee`）。
- 工资结算：`GET /api/payroll?userId=...&from=...&to=...` 返回结算条目与汇总；前端支持 CSV 导出。
- 邮件通知：审批成功的后端处理里调用 `EmailService.SendHtmlAsync(...)`，前端仅显示结果。
- 赛程与事件：复用现有 `events.js`、`matches.js`。

## 权限与角色
- 主办方：有权限创建与管理招募、审核申请、赛程同步、查看结算。
- 人员用户：可浏览任务、提交申请、查看安排与工资。
- 基于现有 `UserRole` 的 `Admin` 作为主办方；后续可扩展专用角色字段（例如用户属性中的 `PersonRole`）。

## 里程碑
1. 初始化项目与基础框架（Vite/Vue/Vuetify、路由与认证、服务层）。
2. 招募任务板与详情、申请流程的前端实现（接口先按约定开发，后端可并行补齐）。
3. 主办方审核与赛程同步的前端界面（对接后端审批与写赛程字段）。
4. 工资结算视图与 CSV 导出；与后端聚合接口联调。
5. 邮件通知反馈与用户体验优化（结果提示、错误处理统一）。
6. 验证与上线：与 `ASG.Api` 联调、测试用例与 E2E 路径、构建与部署。

## 验证与交付
- 统一错误与过期登录处理沿用 `apiFetch` 方案（含 401/403 跳转）。
- 页面级集成测试用假数据与真实接口双模式；上线前切换到生产 `VITE_API_BASE_URL`。
- 首次交付包括：项目脚手架、核心页面与路由、服务层骨架、示例数据视图与 CSV 导出功能。