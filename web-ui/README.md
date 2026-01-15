# Web UI - Vue3 + TypeScript + Vite

## 开发

```bash
npm install
npm run dev
```

## 构建

```bash
npm run build
```

构建输出到 `../wwwroot` 目录，作为嵌入式资源被WPF应用使用。

## 项目结构

```
src/
├── components/          # Vue组件
│   ├── TaskPage.vue           # 文件收集页面
│   ├── DistributionPage.vue   # 在线填表页面
│   └── DepartmentSelector.vue # 部门选择器
├── composables/         # 组合式函数
│   ├── useTheme.ts           # 主题管理
│   └── useDepartments.ts     # 部门数据
├── utils/              # 工具函数
│   └── api.ts                # API请求工具
├── style.css           # 全局样式
├── task.ts             # task.html入口
└── distribution.ts     # distribution.html入口
```

## 多页面配置

项目使用Vite的多页面模式，配置在 `vite.config.ts`:

- `task.html` → 文件收集页面
- `distribution.html` → 在线填表页面
