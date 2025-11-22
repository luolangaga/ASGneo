using System.Reflection;
using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.OpenApi;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Text;
using ASG.Api.Data;
using ASG.Api.Models;
using ASG.Api.Services;
using ASG.Api.Repositories;
using ASG.Api.Middleware;
using ASG.Api.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

// 应用启动入口：配置日志、端口、服务、认证与授权、数据库、路由以及数据导入开关
var builder = WebApplication.CreateBuilder(args);

Log.Information("Bootstrapping ASG.Api. Environment={Environment}", builder.Environment.EnvironmentName);

// 日志：Serilog 控制台/文件输出（含主题与模板）
var theme = new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
{
    [ConsoleThemeStyle.Text] = "\x1b[37m",
    [ConsoleThemeStyle.SecondaryText] = "\x1b[90m",
    [ConsoleThemeStyle.TertiaryText] = "\x1b[90m",
    [ConsoleThemeStyle.Invalid] = "\x1b[31m",
    [ConsoleThemeStyle.Null] = "\x1b[35m",
    [ConsoleThemeStyle.Name] = "\x1b[36m",
    [ConsoleThemeStyle.String] = "\x1b[32m",
    [ConsoleThemeStyle.Number] = "\x1b[33m",
    [ConsoleThemeStyle.Boolean] = "\x1b[33m",
    [ConsoleThemeStyle.Scalar] = "\x1b[36m",
    [ConsoleThemeStyle.LevelVerbose] = "\x1b[90m",
    [ConsoleThemeStyle.LevelDebug] = "\x1b[36m",
    [ConsoleThemeStyle.LevelInformation] = "\x1b[32m",
    [ConsoleThemeStyle.LevelWarning] = "\x1b[33m",
    [ConsoleThemeStyle.LevelError] = "\x1b[31m",
    [ConsoleThemeStyle.LevelFatal] = "\x1b[91m"
});
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(theme: theme, outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 端口绑定：优先使用环境变量 ASPNETCORE_URLS，其次配置项 "Urls"，默认 http://localhost:5250
// 注意：避免与 launchSettings.json 冲突，统一由此处生效
var configuredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                    ?? builder.Configuration["Urls"]
                    ?? "http://localhost:5250";
builder.WebHost.UseUrls(configuredUrls);
Log.Information("Using URLs: {Urls}", configuredUrls);

// 控制器与模型验证：将默认英文错误替换为中文提示
builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new ASG.Api.Filters.ModelStateChineseFilter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(err =>
                    {
                        var m = err.ErrorMessage ?? string.Empty;
                        m = System.Text.RegularExpressions.Regex.Replace(m, "^The field (.+) is required\\.$", "字段 $1 为必填项");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "^The value '(.+)' is not valid\\.$", "值 '$1' 无效");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "^The field (.+) must be a string with a maximum length of (\\d+)\\.$", "字段 $1 的最大长度为 $2");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "^The field (.+) must be a string with a minimum length of (\\d+)\\.$", "字段 $1 的最小长度为 $2");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "^The field (.+) must be between (\\d+) and (\\d+)\\.$", "字段 $1 的取值范围为 $2 到 $3");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "(?i)required", "必填");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "(?i)invalid", "无效");
                        m = System.Text.RegularExpressions.Regex.Replace(m, "(?i)not valid", "无效");
                        return m;
                    }).ToArray()
                );

            var payload = new
            {
                error = new
                {
                    message = "请求参数验证失败",
                    details = errors,
                    timestamp = DateTime.UtcNow
                }
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(payload);
        };
    });

// 反向代理：转发头（X-Forwarded-*）
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Trust all proxies/networks by default. For production, set KnownProxies/KnownNetworks explicitly.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 跨域策略：开发环境放开，非开发环境限定来源
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // 开发环境：开放所有跨域（来源/方法/头）
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // 非开发环境：可根据需要限制来源
            policy.WithOrigins("https://idvevent.cn","https://admin.idvevent.cn","https://person.idvevent.cn")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// 数据库：PostgreSQL 连接与上下文池
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
);

// 身份与密码策略：长度、复杂度、唯一邮箱等
builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT：令牌验证与 SignalR QueryToken 支持（/hubs/* 允许 query 中携带 access_token）
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings?.Issuer,
            ValidAudience = jwtSettings?.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? ""))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Register services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IArticleRepository, ArticleRepository>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRecruitmentRepository, RecruitmentRepository>();
builder.Services.AddScoped<IRecruitmentService, RecruitmentService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient();
// 远端数据抓取 HttpClient：网易接口（自动解压 gzip/deflate/br）
builder.Services.AddHttpClient("netease", c =>
{
    c.BaseAddress = new Uri("https://h55.s3.game.163.com");
    c.DefaultRequestHeaders.Add("Accept", "application/json");
    c.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, br");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
});
builder.Services.AddSingleton<IAiService, AiService>();
// 后台任务：周期性抓取网易英雄统计并写入数据库
builder.Services.AddHostedService<NeteaseHeroStatsIngestService>();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Configure authorization policies
AuthorizationPolicies.ConfigurePolicies(builder.Services);

// OpenAPI 与文档：Swagger 输出 JSON + Scalar UI 展示
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ASG API Framework", Version = "v1" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 构建 Web 应用（依赖注入容器已就绪）
var app = builder.Build();
Log.Information("WebApplication built successfully.");


// 命令行工具：将 SQLite 迁移到 PostgreSQL（一次性执行）
if (args.Contains("--migrate-sqlite-to-pg"))
{
    using var scope = app.Services.CreateScope();
    var sqlite = builder.Configuration.GetConnectionString("Sqlite") ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ASGApiDb.db";
    var pg = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=asg;Username=postgres;Password=postgres";
    await ASG.Api.Services.DataMigrationService.MigrateSqliteToPostgres(sqlite, pg);
    return;
}
// 路由：OpenAPI JSON（/openapi/v1.json）与 Scalar UI
app.UseSwagger(options => { options.RouteTemplate = "openapi/{documentName}.json"; });
app.MapScalarApiReference(options =>
{
    options.Title = "ASG API Doc";
    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
});

// 自定义中间件：统一异常处理
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 反向代理转发：必须在可能使用 Host/Scheme 的中间件之前调用
app.UseForwardedHeaders();

// HTTPS：非开发环境启用重定向（开发环境免证书，简化本地启动）
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("DevCors");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// 实时通信：SignalR Hub（JWT 鉴权；前端可用 query 参数 access_token）
app.MapHub<ASG.Api.Hubs.AppHub>("/hubs/app");
Log.Information("SignalR hub mapped: Path={Path}, AuthScheme=JWT Bearer, QueryToken=access_token for '/hubs/*'", "/hubs/app");
if (app.Environment.IsDevelopment())
{
    Log.Information("CORS policy 'DevCors' applied: AllowAnyOrigin/AnyHeader/AnyMethod (Development)");
}
else
{
    Log.Information("CORS policy 'DevCors' applied: Restricted origins [https://idvevent.cn, https://admin.idvevent.cn, https://person.idvevent.cn]");
}

// 数据库初始化：打印提供者信息、打补丁/迁移、按配置执行本地数据导入
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // 打印当前使用的 EF 提供者与数据库名（便于排查导入目标库）
    try
    {
        var provider = context.Database.ProviderName;
        var dbName = context.Database.GetDbConnection().Database;
        Log.Information("EF provider: {Provider}, database: {Database}", provider, dbName);
    }
    catch { }
    // 先尝试直接给 Events 表添加 CustomData 列（幂等），失败则走 EF Migrate
    try
    {
        // 尝试直接为 Events 表添加 CustomData 列（幂等）
        context.Database.ExecuteSqlRaw("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"CustomData\" text NOT NULL DEFAULT json_build_object()::text ");
        Log.Information("Applied schema patch for Events.CustomData.");
    }
    catch (Exception patchEx)
    {
        Log.Warning(patchEx, "Schema patch failed; attempting EF Core migrations for new databases.");
        try
        {
            context.Database.Migrate();
            Log.Information("Database migrated successfully after patch failure.");
        }
        catch (Exception migEx)
        {
            Log.Error(migEx, "Database migration failed after patch failure.");
            // 不中断启动（部分环境可能已有旧库），继续后续流程
        }
    }

    // 本地导入参数（支持 appsettings / 环境变量 DataSeed__* / 命令行）：
    // - ImportNeteaseHeroesFromLocal：是否导入英雄（默认 true）
    // - OverwriteNeteaseHeroes：导入前是否清空英雄表（默认 false）
    // - ImportNeteaseStatsFromLocal：是否导入统计（默认 true）
    // - OverwriteNeteaseStats：导入前是否清空统计表（默认 false）
    var importLocalHeroes = builder.Configuration.GetValue<bool?>("DataSeed:ImportNeteaseHeroesFromLocal") ?? true;
    var overwriteLocalHeroes = builder.Configuration.GetValue<bool?>("DataSeed:OverwriteNeteaseHeroes") ?? false;
    var importLocalStats = builder.Configuration.GetValue<bool?>("DataSeed:ImportNeteaseStatsFromLocal") ?? true;
    var overwriteLocalStats = builder.Configuration.GetValue<bool?>("DataSeed:OverwriteNeteaseStats") ?? false;
    Log.Information("Local heroes import setting: {Enabled}", importLocalHeroes);
    Log.Information("Local heroes overwrite setting: {Enabled}", overwriteLocalHeroes);
    Log.Information("Local stats import setting: {Enabled}", importLocalStats);
    Log.Information("Local stats overwrite setting: {Enabled}", overwriteLocalStats);
    // 导入：英雄基础信息（幂等新增/更新，支持覆盖清空）
    if (importLocalHeroes)
    {
        try
        {
            await DataSeeder.SeedNeteaseHeroesFromLocalAsync(context, overwriteLocalHeroes);
        }
        catch (Exception se)
        {
            Log.Warning(se, "Netease heroes seeding failed; continuing startup.");
        }
    }
    else
    {
        Log.Information("Local heroes import skipped by setting.");
    }

    // 导入：周度/赛季统计（去重判断，支持覆盖清空）
    if (importLocalStats)
    {
        try
        {
            await DataSeeder.SeedNeteaseStatsFromLocalAsync(context, overwriteLocalStats);
        }
        catch (Exception se)
        {
            Log.Warning(se, "Netease stats seeding failed; continuing startup.");
        }
    }
    else
    {
        Log.Information("Local stats import skipped by setting.");
    }

    // 开发环境演示数据（赛事/战队/管理员），生产环境不执行
    if (app.Environment.IsDevelopment())
    {
        try
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await DataSeeder.SeedDevDataAsync(context, userManager);
        }
        catch (Exception se)
        {
            Log.Warning(se, "Dev data seeding failed; continuing startup without seed.");
        }
    }
}

// 启动 Kestrel 服务
Log.Information("Starting Kestrel...");
try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly during Kestrel startup");
    throw;
}
