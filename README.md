# CommitCraft - AI Code Review Platform

一个基于 AI 的代码审查平台，支持 GitHub / Gitee，支持自动问题分配、实时通知、定时审核计划。

## 功能特性

- 🤖 **AI 智能审查** — 自动分析代码问题（安全、正确性、性能、可维护性等）
- 🔄 **自动分配** — 根据 Git 提交者名称自动分配审核任务
- 📊 **数据仪表盘** — KPI 卡片、趋势图、问题分布统计
- ⏰ **定时审核** — 支持 Cron 表达式配置定时代码审查
- 🔔 **实时通知** — 审核完成即时推送（SignalR）
- 👥 **多平台支持** — GitHub / Gitee
- 🏷️ **问题管理** — 严重程度分类、批量处理、追踪记录
- 👤 **用户管理** — 角色权限控制（管理员 / 审核员 / 开发者）

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + ECharts |
| 后端 | .NET 8 + SqlSugar + MySQL + SignalR |
| AI 模型 | MiniMax-M2 |

## 项目结构

```
code-review/
├── Api/                    # .NET 后端
│   └── Controllers/        # API 控制器
├── Application/            # 应用层
│   ├── Entity/             # 数据实体
│   ├── IService/           # 服务接口
│   └── Service/            # 服务实现
├── Domain/                 # 领域层
└── frontend/               # Vue 前端
    └── src/
        ├── api/            # API 调用
        ├── views/          # 页面组件
        └── router/         # 路由配置
```

## 快速启动

### 前置依赖

- .NET 8 SDK
- Node.js 18+
- MySQL 8.0+

### 后端启动

```bash
cd Api
dotnet run
# 访问 http://localhost:8080
```

### 前端启动

```bash
cd frontend
npm install
npm run dev
# 访问 http://localhost:6060
```

### 初始化数据库

首次启动时后端自动创建数据库和表。默认管理员账号：`admin` / `admin123`

## License

MIT
