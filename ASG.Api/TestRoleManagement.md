# 角色管理功能测试指南

## 概述
本文档提供了完整的角色管理功能测试指南，包括用户注册、登录、角色分配和权限验证。

## 角色说明
- **User (用户)**: 基础用户角色，默认角色
- **Admin (管理员)**: 可以管理用户，查看报告
- **SuperAdmin (超级管理员)**: 拥有所有权限，包括分配角色、删除用户、系统管理

## API 端点测试

### 1. 用户注册和登录

#### 注册新用户
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "firstName": "张",
  "lastName": "三",
  "role": "User"
}
```

#### 用户登录
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}
```

### 2. 角色管理 API

#### 获取所有角色 (需要登录)
```http
GET /api/role/roles
Authorization: Bearer {token}
```

#### 更新用户角色 (仅超级管理员)
```http
PUT /api/role/users/{userId}/role
Authorization: Bearer {token}
Content-Type: application/json

{
  "newRole": "Admin"
}
```

#### 根据角色获取用户列表 (管理员及以上)
```http
GET /api/role/users-by-role/Admin
Authorization: Bearer {token}
```

#### 分页获取用户列表 (管理员及以上)
```http
GET /api/role/users?pageNumber=1&pageSize=10
Authorization: Bearer {token}
```

#### 获取角色统计信息 (仅超级管理员)
```http
GET /api/role/statistics
Authorization: Bearer {token}
```

#### 检查用户权限
```http
GET /api/role/check-permission/{permission}
Authorization: Bearer {token}
```

#### 获取当前用户角色信息
```http
GET /api/role/my-role
Authorization: Bearer {token}
```

### 3. 用户管理 API

#### 获取当前用户资料
```http
GET /api/users/profile
Authorization: Bearer {token}
```

#### 获取所有用户 (管理员及以上)
```http
GET /api/users
Authorization: Bearer {token}
```

#### 删除用户 (仅超级管理员)
```http
DELETE /api/users/{userId}
Authorization: Bearer {token}
```

## 测试场景

### 场景1: 基础用户权限测试
1. 注册一个普通用户 (role: "User")
2. 登录获取 JWT token
3. 尝试访问管理员功能 (应该返回 403 Forbidden)
4. 验证只能访问自己的资料

### 场景2: 管理员权限测试
1. 创建一个管理员用户 (需要超级管理员分配角色)
2. 登录管理员账户
3. 验证可以查看用户列表
4. 验证可以查看报告
5. 验证不能分配角色或删除用户

### 场景3: 超级管理员权限测试
1. 创建超级管理员用户
2. 验证可以分配角色
3. 验证可以删除用户
4. 验证可以访问所有功能

### 场景4: 权限验证测试
1. 测试不同角色访问受保护端点
2. 验证 JWT token 中包含正确的角色信息
3. 测试角色层级权限 (SuperAdmin > Admin > User)

## 预期结果

### 成功响应示例

#### 登录成功响应
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "firstName": "张",
    "lastName": "三",
    "fullName": "张三",
    "role": "User",
    "roleDisplayName": "用户",
    "roleName": "User",
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z",
    "isActive": true
  }
}
```

#### 角色统计响应
```json
{
  "User": 10,
  "Admin": 3,
  "SuperAdmin": 1
}
```

### 错误响应示例

#### 权限不足 (403 Forbidden)
```json
{
  "message": "Access denied. Insufficient permissions."
}
```

#### 未授权 (401 Unauthorized)
```json
{
  "message": "Unauthorized access."
}
```

## 注意事项

1. 所有需要认证的端点都需要在 Header 中包含 `Authorization: Bearer {token}`
2. JWT token 包含用户角色信息，用于服务端权限验证
3. 角色权限是层级的：SuperAdmin > Admin > User
4. 默认注册的用户角色为 User
5. 只有 SuperAdmin 可以分配角色和删除用户
6. 管理员可以查看用户列表和报告，但不能修改用户角色

## 测试工具

推荐使用以下工具进行测试：
- Swagger UI: http://localhost:5250/swagger
- Postman
- curl 命令行工具
- ASG.Api.http 文件 (VS Code REST Client)