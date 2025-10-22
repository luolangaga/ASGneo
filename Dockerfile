# 使用官方 .NET 8.0 SDK 镜像作为构建阶段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖项
COPY ["ASG.Api/ASG.Api.csproj", "ASG.Api/"]
RUN dotnet restore "ASG.Api/ASG.Api.csproj"

# 复制所有源代码
COPY . .
WORKDIR "/src/ASG.Api"

# 构建应用程序
RUN dotnet build "ASG.Api.csproj" -c Release -o /app/build

# 发布应用程序
FROM build AS publish
RUN dotnet publish "ASG.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 使用官方 .NET 8.0 运行时镜像作为最终阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 创建非 root 用户
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# 从发布阶段复制应用程序
COPY --from=publish /app/publish .

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# 暴露端口
EXPOSE 8080

# 健康检查
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# 设置入口点
ENTRYPOINT ["dotnet", "ASG.Api.dll"]