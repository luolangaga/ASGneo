# ASG API 构建指南

本文档详细说明了如何构建和部署 ASG API 项目，包括本地构建、GitHub Actions 自动构建和 Docker 部署。

## 📋 目录

- [本地构建](#本地构建)
- [GitHub Actions 自动构建](#github-actions-自动构建)
- [Docker 部署](#docker-部署)
- [配置说明](#配置说明)
- [故障排除](#故障排除)

## 🔨 本地构建

### 前置要求

- .NET 8.0 SDK
- Git

### 构建步骤

```bash
# 克隆项目
git clone <your-repository-url>
cd ASG

# 还原依赖项
dotnet restore ASG.Api

# 构建项目
dotnet build ASG.Api --configuration Release

# 运行测试
dotnet test ASG.Api

# 发布应用程序
# Windows x64
dotnet publish ASG.Api -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-x64

# Linux x64
dotnet publish ASG.Api -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-x64
```

## 🚀 GitHub Actions 自动构建

### 工作流程说明

项目包含完整的 GitHub Actions 工作流 (`.github/workflows/build-and-deploy.yml`)，支持：

- **多平台构建**: Windows x64 和 Linux x64
- **Docker 镜像构建**: 支持 AMD64 和 ARM64 架构
- **自动测试**: 在构建前运行单元测试
- **制品上传**: 自动上传构建结果
- **自动发布**: 在创建 Release 时自动发布

### 触发条件

- **推送到主分支**: `main` 或 `develop`
- **Pull Request**: 针对 `main` 分支
- **创建 Release**: 自动构建并发布

### 配置 Secrets

在 GitHub 仓库设置中添加以下 Secrets：

```
DOCKER_USERNAME=your-docker-hub-username
DOCKER_PASSWORD=your-docker-hub-password
```

### 构建产物

每次构建会生成以下制品：

- `asg-api-windows-x64`: Windows 可执行文件
- `asg-api-linux-x64`: Linux 可执行文件
- Docker 镜像推送到 Docker Hub

## 🐳 Docker 部署

### 本地构建 Docker 镜像

```bash
# 构建镜像
docker build -t asg-api:latest .

# 运行容器
docker run -d \
  --name asg-api \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  asg-api:latest
```

### 使用 Docker Compose

创建 `docker-compose.yml` 文件：

```yaml
version: '3.8'

services:
  asg-api:
    image: your-username/asg-api:latest
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./appsettings.Production.json:/app/appsettings.json:ro
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

运行：

```bash
docker-compose up -d
```

### 从 Docker Hub 拉取

```bash
# 拉取最新版本
docker pull your-username/asg-api:latest

# 拉取特定版本
docker pull your-username/asg-api:v1.0.0
```

## ⚙️ 配置说明

### 环境变量

| 变量名 | 描述 | 默认值 |
|--------|------|--------|
| `ASPNETCORE_ENVIRONMENT` | 运行环境 | `Production` |
| `ASPNETCORE_URLS` | 监听地址 | `http://+:8080` |

### 配置文件

确保在生产环境中正确配置 `appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ASGApiDb.db"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "ASG.Api",
    "Audience": "ASG.Api.Users",
    "ExpirationInMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## 🔧 故障排除

### 常见问题

1. **构建失败**
   - 检查 .NET SDK 版本是否为 8.0
   - 确保所有依赖项已正确还原

2. **Docker 构建失败**
   - 检查 Dockerfile 路径是否正确
   - 确保 .dockerignore 文件配置正确

3. **GitHub Actions 失败**
   - 检查 Secrets 是否正确配置
   - 查看构建日志了解具体错误

4. **容器启动失败**
   - 检查端口是否被占用
   - 验证配置文件格式是否正确
   - 查看容器日志：`docker logs asg-api`

### 健康检查

应用程序提供健康检查端点：

```bash
# 检查应用程序状态
curl http://localhost:8080/health
```

### 日志查看

```bash
# 查看容器日志
docker logs asg-api

# 实时查看日志
docker logs -f asg-api
```

## 📝 版本发布

1. 创建新的 Git 标签：
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. 在 GitHub 上创建 Release

3. GitHub Actions 会自动：
   - 构建所有平台版本
   - 创建 Docker 镜像
   - 上传构建制品到 Release

## 🤝 贡献

如需改进构建流程，请：

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 创建 Pull Request

---

更多信息请参考项目文档或联系维护战队。