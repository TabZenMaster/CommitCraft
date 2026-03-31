using CodeReview.Application.IService;
using CodeReview.Application.Service;
using CodeReview.Api.Hubs;
using CodeReview.Api.Services;
using CodeReview.Domain.Entities;
using CodeReview.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.Net.Http;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// ===== SqlSugar (MySQL) =====
var connStr = AppDbContext.ConnectionString;
builder.Services.AddSingleton<ISqlSugarClient>(sp =>
{
    var config = new ConnectionConfig
    {
        ConnectionString = connStr,
        DbType = DbType.MySql,
        IsAutoCloseConnection = true,
        MoreSettings = new ConnMoreSettings { IsAutoToUpper = false }
    };
    return new SqlSugarScope(config);
});

// ===== HttpClient =====
builder.Services.AddHttpClient();

// ===== Services =====
builder.Services.AddSingleton<ReviewQueueService>();
builder.Services.AddHostedService<ReviewBackgroundService>();
builder.Services.AddScoped<ISysUserService, SysUserService>();
builder.Services.AddScoped<IModelConfigService, ModelConfigService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

// ===== JWT =====
var jwtKey = "CodeReview_Secret_Key_2026_Must_Be_32_Chars!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CodeReview",
            ValidAudience = "CodeReview",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// ===== SignalR =====
builder.Services.AddSignalR();

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("allow", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== 数据库初始化建表 =====
InitDatabase(app.Services);

// ===== 中间件 =====
app.UseCors("allow");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run("http://0.0.0.0:8080");

// ===== 数据库建表 + 初始化管理员 =====
void InitDatabase(IServiceProvider sp)
{
    var db = sp.GetRequiredService<ISqlSugarClient>();
    db.CodeFirst.InitTables<ModelConfig>();
    db.CodeFirst.InitTables<Repository>();
    db.CodeFirst.InitTables<ReviewCommit>();
    db.CodeFirst.InitTables<ReviewResult>();
    db.CodeFirst.InitTables<HandlerLog>();
    db.CodeFirst.InitTables<SysUser>();

    var exists = db.Queryable<SysUser>().Where(x => x.Username == "admin" && !x.IsDeleted).Any();
    if (!exists)
    {
        db.Insertable(new SysUser
        {
            Username = "admin",
            Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
            RealName = "管理员",
            Role = "admin",
            Status = 1,
            CreateTime = DateTime.Now,
            IsDeleted = false
        }).ExecuteCommand();
    }
}
