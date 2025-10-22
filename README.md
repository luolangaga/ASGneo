## ASG项目
### 项目简介
本项目基于 **.NET 10.0**，提供高性能、可扩展的企业级解决方案。同时以MIT协议开源，欢迎贡献代码。与老版ASG项目不同，本项目注重代码质量、可维护性和可扩展性。
与neo-bp有一些联动，该项目与neo-bp共同构建起第五人格赛事办赛一键式解决方案。
### 技术栈
- **运行时**：.NET 10.0  
- **语言**：C# 12  
- **Web 框架**：ASP.NET Core
- **依赖注入**：原生 DI 容器  
- **ORM**：EF Core 10
- **数据库**：SQLite
- **认证**：JWT（JSON Web Token）  
- **授权**：基于角色的权限管理  
### 部署
#### 通过源码部署
1. 确保已安装 .NET 10.0 SDK。
2. 克隆项目代码：`git clone https://github.com/luolangaga/ASG.git`
3. 导航到项目目录：`cd ASG`
4. 恢复依赖项：`dotnet restore`
5. 运行项目：`dotnet run`
#### 在Windows部署
1. 确保已安装 .NET 10.0 Runtime。
2. 运行项目：`dotnet ASG.exe`
#### 在Linux部署
1. 确保已安装 .NET 10.0 Runtime。
2. 运行项目：`dotnet ASG.dll`
#### Nginx配置
1. 安装Nginx。
2. 配置Nginx反向代理，将请求转发到ASG项目。
```nginx
server {
    listen 80;
    server_name yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```
### 项目结构
- **Controllers**：处理HTTP请求的控制器。
- **Models**：定义数据模型。
- **Services**：实现业务逻辑。
- **Data**：包含数据库上下文和迁移文件。
- **wwwroot**：静态文件目录（如HTML、CSS、JS）。
- **Program.cs**：应用程序入口点。
- **appsettings.json**：应用程序配置文件。
### 项目前身
项目前身是 https://idvasg.cn。因为历史原因，该项目急需重构，以适应新的需求和技术趋势，所以该项目就诞生了。
### 贡献
我们欢迎所有形式的贡献，包括但不限于代码、文档、问题报告、功能建议等。请通过提交Pull Request或打开Issue来参与贡献。
### 协议
本项目基于MIT协议开源，您可以在遵守协议的前提下自由使用、修改和分发本项目的代码。
### 联系我们
如果您有任何问题、建议或合作意向，请通过以下方式联系我们：
- **邮箱**：luolan233@outlook.com