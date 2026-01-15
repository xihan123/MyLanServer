# MyLanServer - 内网文件分发与收集工具

<div align="center">

**MyLanServer** 是一个基于 **WPF + ASP.NET Core** 的局域网文件/数据收集工具，采用**双主机架构**，将桌面应用与 Web 服务器完美融合。

[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![Offline](https://img.shields.io/badge/Offline-Ready-green.svg)](README.md#快速开始)

[功能特性](#功能详解) • [快速开始](#快速开始) • [API 文档](#web-接口文档) • [常见问题](#常见问题)

</div>

---

## 项目概述

MyLanServer 是专为内网环境设计的文件和数据收集工具，无需安装额外服务器软件，即可快速搭建文件收集系统。

### 核心特性

- **双任务模式**：支持文件收集和在线填表两种模式
- **智能合并**：Excel 文件版本控制、多字段去重、统计报表
- **安全可靠**：密码保护、文件类型验证、提交限制
- **组织管理**：完整的"部门管理"和"人员管理"体系
- **一键部署**：单文件发布，开箱即用
- **双主机架构**：WPF 桌面应用与嵌入式 Web 服务器共享服务实例

### 技术亮点

- **双主机架构**：WPF 和 Kestrel 共享 DI 容器，无需进程间通信
- **版本控制**：自动识别文件版本号，智能选择最新文件
- **身份证识别**：自动从身份证号提取年龄、性别、出生日期
- **IO 锁机制**：全局并发控制，防止文件冲突
- **魔数验证**：防止文件类型伪装，增强安全性

---

## 快速开始

### 环境要求

**运行环境**：

- Windows 10/11 或 Windows Server 2016
- .NET 9.0 Runtime（框架依赖版本）或无需运行时（自包含版本）

**开发环境**：

- .NET 9.0 SDK
- Visual Studio 2022 或 JetBrains Rider

### 安装与运行

#### 方式一：下载预编译版本（推荐）

1. 从 [GitHub Releases](https://github.com/xihan123/MyLanServer/releases) 下载最新版本
2. 解压压缩包到任意目录
3. 双击运行 MyLanServer.exe
4. 在主界面点击"启动"按钮

#### 方式二：从源码编译

```bash
# 克隆仓库
git clone https://github.com/xihan123/MyLanServer.git
cd MyLanServer

# 编译并运行
dotnet build
dotnet run
```

#### 方式三：发布单文件可执行程序

```bash
# 发布为自包含单文件（包含 .NET 运行时，较大但无需安装运行时）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 发布为框架依赖单文件（需要安装 .NET 9.0 Runtime，体积小）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:SelfContained=false -p:EnableCompressionInSingleFile=false
```

### 基本使用流程

#### 1. 启动服务器

1. 运行应用后，在主界面设置端口（默认 8080）
2. 点击"启动服务器"按钮启动 Web 服务器
3. 服务器将绑定到所有网络接口（0.0.0.0）

#### 2. 创建收集任务

1. 点击"新建任务"按钮
2. 选择任务类型：
    - **文件收集**：用户下载 Excel 模板填写后上传
    - **在线填表**：用户在线填写表单，数据以 JSON 保存
3. 配置任务选项：
    - 上传 Excel 模板（文件收集任务）
    - 配置表单字段（在线填表任务）
    - 设置密码保护（可选）
    - 设置提交上限（可选）
    - 设置过期时间（可选）
    - 启用附件上传（可选）
4. 点击"确定"创建任务

#### 3. 分享任务链接

1. 在任务列表中找到您的任务
2. 右键点击任务，选择"复制链接"
3. 将链接发送给需要提交的用户

任务链接格式：

```
http://服务器IP:端口/task.html?slug=14位Slug      # 文件收集
http://服务器IP:端口/distribution.html?slug=14位Slug  # 在线填表
```

#### 4. 查看提交

1. 右键点击任务，选择"打开提交列表"
2. 查看所有提交记录（姓名、联系方式、部门、提交时间）
3. 用户提交的文件或查看数据

#### 5. 合并文件（可选）

1. 右键点击任务  "合并文件"
2. 选择源文件夹（包含所有提交文件）
3. 配置合并选项（去重字段、表头行、分隔符）
4. 点击"开始合并"生成合并后的 Excel 文件

---

## 功能详解

### 1. 任务管理

#### 创建文件收集任务

1. 点击"新建任务"按钮
2. 任务类型选择"文件收集"
3. 填写任务标题和描述
4. 上传 Excel 模板文件（用户将下载此模板填写）
5. 配置任务选项：
    - **密码保护**：设置访问密码（可选）
    - **提交上限**：限制提交数量（0 表示无限制）
    - **一次性限制**：总共只能下载一次，下载后其他人无法下载
    - **过期时间**：设置任务失效时间（长期有效或指定时间）
    - **接口公开**：在 API 中显示任务描述
    - **附件上传**：允许用户提交附件
    - **附件说明**：附件上传的提示文本
6. 点击"确定"创建任务

#### 创建在线填表任务

1. 点击"新建任务"按钮
2. 任务类型选择"在线填表"
3. 填写任务标题和描述
4. 配置表单字段：
    - 字段名称
    - 字段类型
    - 是否必填
5. 配置任务选项（同文件收集任务）
6. 点击"确定"创建任务

#### 任务配置选项详解

- **密码保护**：用户访问任务时需要输入密码，密码使用 PBKDF2 哈希存储
- **提交上限**：限制总提交数量，达到上限后自动停止接收
- **一次性限制**：暂无实现
- **过期时间**：任务过期后自动停止接收，支持快捷时间选项
- **接口公开**：在 API 响应中包含任务描述
- **附件上传**：允许用户在提交时上传附件
- **附件说明**：显示在附件上传区域的提示文本

#### 编辑任务

1. 右键点击任务 选择"编辑任务"
2. 可修改描述、密码、过期时间等设置
3. 修改后不会生成新的访问链接（Slug 保持不变）

#### 复刻任务

1. 右键点击任务 选择"复刻分发"
2. 输入新任务标题（默认为原标题  "副本"）
3. 创建任务副本，保留原设置
4. 生成新的访问链接，适合创建相似的任务

**复刻功能会**：

- 复制所有任务配置（密码、限制、过期时间等）
- 复制表单 Schema（在线填表任务）
- 创建新的目录结构
- 复制任务附件（如果有）
- 生成新的 14 位 Slug

#### 删除任务

1. 右键点击任务 选择"删除任务"
2. 可选择是否同时删除物理文件夹
3. 删除后数据无法恢复，请谨慎操作

#### 任务状态管理

- **激活**：任务正常接收提交
- **停用**：任务暂停接收提交（临时禁用）
- **已过期**：超过过期时间，自动停止接收

切换方法：

- 右键任务  "启用/禁用"
- 或直接在任务列表中勾选/取消"激活"列

---

### 2. 文件收集功能

#### 工作流程

管理员创建任务 上传 Excel 模板

生成任务链接 分享给用户

用户访问链接 下载 Excel 模板

填写 Excel 文件 上传文件

服务器验证 保存文件

记录提交 数据库

#### 模板管理

- **模板文件存储位置**：config/{任务标题}/{模板文件名}.xlsx
- **模板文件可更换**：在任务配置中上传新模板，不影响已创建的任务
- **支持的文件格式**：.xlsx, .xls, .csv

#### 文件提交与验证

**提交流程**：

1. 用户访问任务链接
2. 输入姓名、联系方式、部门（必填）
3. 输入密码（如果设置了密码保护）
4. 下载 Excel 模板
5. 填写 Excel 文件
6. 上传填写好的文件
7. 可选：上传附件（如果启用）
8. 提交成功

**服务器验证**：

- 任务存在性和激活状态
- 过期时间检查
- 密码验证（PBKDF2 哈希比对）
- 提交上限检查
- 一次性限制检查（基于联系方式）
- 部门存在性验证
- 文件类型验证（魔数验证）
- 文件大小限制（单文件 50MB）

**文件保存**：

- 保存位置：收集/{任务标题}/{Slug}/文件收集/
- 文件命名：模板名-姓名-所属部门_v版本号-时间戳.xlsx
- 版本号格式：v1, v2, v3 ...（自动递增）
- IO 锁保护：防止并发写入冲突

#### 版本控制机制

**自动版本模式**（推荐）：

- 用户多次提交时自动递增版本号
- 文件名示例：报销单-张三-技术部_v1-20250111_143025.xlsx
- 合并时自动选择最新版本

**覆盖模式**：

- 用户多次提交时覆盖旧文件
- 不保留历史版本

---

### 3. 在线填表功能

#### 表单设计

**字段配置**（在任务配置中设置）：

```json
{
  "title": "在线表格分发测试",
  "columns": [
    {
      "name": "遗失数量",
      "type": "数字",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "回收数量",
      "type": "数字",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "已申报",
      "type": "双选框(是/否)",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "已处理",
      "type": "双选框(是/否)",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    }
  ]
}
```

#### 数据存储格式

**JSON 文件保存**：

- 保存位置：收集/{任务标题}/{Slug}/在线填表/
- 文件命名：姓名_所属部门_时间戳.json
- 内容示例：

```json
{
  "遗失数量": 14,
  "回收数量": 57,
  "已申报": "false",
  "已处理": "false",
  "所属部门": "技术部"
}
```

#### 附件支持

**管理员附件**：

- 随任务发布，用户可下载
- 用途：提供参考文档、示例文件等
- 上传位置：任务配置 附件管理
- 文件路径: 收集/{任务标题}/{Slug}/attachments

**用户附件**：

- 用户提交表单时可上传
- 保存在：收集/{任务标题}/{Slug}/提交人-所属部门/
- 文件命名：字段名_v版本号.扩展名

---

### 4. Excel 文件合并

#### 版本选择合并

**自动版本识别**：

- 从文件名中提取版本号：_v数字-
- 按姓名联系方式分组
- 选择每组中版本号最大的文件
- 适用于用户多次提交更新的场景

**文件名格式**：

模板名-姓名-所属部门_v版本号-时间戳.xlsx
例如：报销单-张三-技术部v1-20250111_143025.xlsx

#### 多字段去重

**工作原理**：

1. 选择多个字段作为去重依据（如"姓名""联系方式"）
2. 将字段值用分隔符连接
3. 相同组合只保留一条记录
4. 自定义分隔符（默认为 ）

**示例**：

- 去重字段：姓名 联系方式
- 记录 A：张三 13800138000
- 记录 B：张三 13800138000
- 结果：只保留一条记录（通常选择版本号较大的）

#### 合并步骤

1. **准备工作**
    - 确保所有提交文件在同一个文件夹
    - 准备好模板文件（可选，用于列匹配）

2. **打开合并对话框**
    - 方式一：右键任务  "合并文件"
    - 方式二：工具栏  "文件合并"

3. **配置选项**
    - **源文件夹**：选择包含提交文件的文件夹
    - **输出文件**：指定合并后的保存位置
    - **模板文件**：选择模板文件用于列匹配（可选）
    - **表头行**：指定表头所在行（从 0 开始，默认第 0 行）
    - **去重字段**：选择用于去重的列
    - **分隔符**：多字段组合去重时使用的分隔符（默认 ）

4. **开始合并**
    - 点击"开始合并"按钮
    - 等待合并完成
    - 查看合并统计信息（总文件数、去重后数量、合并数量）
    - 打开输出文件夹查看结果

#### 统计合并模式（在线填表）

**累计模式**：

- 统计每个选项的出现次数
- 示例：性别 字段 男【20】，女【15】

**分组统计模式**：

- 按指定字段分组统计
- 示例：按"部门"分组统计"性别"  男【技术部：10，销售部：8】，女【技术部：5，销售部：7】

---

### 5. 部门与人员管理

#### 部门管理

**功能**：

- 添加部门
- 编辑部门名称
- 删除部门
- 部门排序（上移/下移/置顶）
- 导入部门（Excel）
- 导出部门（Excel）

**导入格式**：
Excel 表头：部门名称（或 Name）

部门名称
技术部
销售部
财务部

#### 人员管理

**功能**：

- 添加人员
- 编辑人员信息
- 删除人员
- 搜索人员（支持姓名、身份证、联系方式搜索）
- 导入人员（Excel）
- 导出人员（Excel）

**字段说明**：

| 字段    | 说明       | 必填 | 自动计算           |
|-------|----------|----|----------------|
| 姓名    | 人员姓名     | 是  | -              |
| 身份证号  | 18 位身份证号 | 是  | 自动计算年龄、性别、出生日期 |
| 联系方式1 | 手机号或电话   | 否  | -              |
| 联系方式2 | 备用联系方式   | 否  | -              |
| 部门    | 所属部门     | 否  | -              |
| 职位    | 职务       | 否  | -              |
| 员工编号  | 工号       | 否  | -              |
| 注册地址  | 身份证地址    | 否  | -              |
| 现住址   | 当前居住地址   | 否  | -              |
| 入职日期  | 参加工作日期   | 否  | -              |

**身份证自动识别**：

- 自动提取年龄
- 自动提取性别（1男，2女）
- 自动提取出生日期（YYYY-MM-DD 格式）

**导入格式**：
Excel 表头：姓名, 身份证号, 联系方式1, 联系方式2, 部门, 职位, 员工编号, 注册地址, 现住址, 入职日期
（支持多列名映射，如 Name 姓名）

---

### 6. 附件管理

#### 任务附件

**用途**：

- 提供参考文档
- 提供示例文件
- 提供填写说明

**上传**：

- 任务配置 附件管理 添加附件
- 支持多个附件

**排序**：

- 支持上移/下移/置顶
- 显示顺序影响用户下载列表

**下载**：

- 用户在任务页面查看附件列表
- 点击下载按钮获取文件

#### 用户附件

**启用方式**：

- 任务配置 勾选"允许上传附件"
- 设置"附件说明"提示用户

**上传限制**：

- 单文件大小限制：50MB
- 文件类型验证（魔数验证）
- 保存在：收集/{任务标题}/{Slug}/attachments/提交人-所属部门/

**版本控制**：

- 多次上传自动递增版本号
- 文件命名：字段名_v版本号.扩展名

---

## Web 接口文档

### 认证与安全

**任务验证中间件**：

- 所有任务相关接口都会经过 TaskValidationMiddleware 验证
- 从 URL 提取 14 位 Slug
- 验证任务存在性、激活状态、过期时间、提交上限、一次性限制
- 将 LanTask 存入 HttpContext.Items["Task"]

**密码验证**：

- 密码使用 PBKDF2 哈希（10000 迭代，SHA-256）
- 前端传输后立即哈希，不在网络中传输明文

**文件验证**：

- Magic Number 验证防止文件类型伪装
- 支持的文件类型：Office、PDF、图片、压缩包等
- 拒绝可执行文件（.exe, .bat, .cmd 等）

### 文件收集接口

#### 获取任务信息

GET /api/task/{slug}/info

**响应**：

```json
{
  "id": "d4526ba0-40d4-406c-a730-302479653043",
  "slug": "3MCS7MFE47G6DD",
  "title": "测试文件收集分发",
  "description": "测试文件收集分发描述",
  "taskType": 0,
  "hasPassword": true,
  "maxLimit": 100,
  "currentCount": 0,
  "expiryDate": "2026-01-16T15:06:06",
  "versioningMode": 1,
  "hasAttachment": false,
  "allowAttachmentUpload": true,
  "attachmentDownloadDescription": null,
  "allowedExtensions": [
    ".pdf",
    ".doc",
    ".docx",
    ".xls",
    ".xlsx",
    ".txt",
    ".jpg",
    ".jpeg",
    ".png",
    ".gif",
    ".bmp",
    ".zip",
    ".rar",
    ".7z"
  ],
  "isActive": true,
  "isExpired": false,
  "isLimitReached": false
}
```

#### 下载模板

GET /api/template/{slug}

**响应**：

- 成功：返回 Excel 模板文件
- 失败：返回错误信息（任务不存在、未激活、已过期等）

#### 提交文件

POST /api/submit/{slug}
Content-Type: multipart/form-data

**请求参数**：

- file: Excel 文件（必填）
- submitterName: 提交人姓名（必填）
- contact: 联系方式（必填）
- department: 部门（必填）
- password: 密码（如果任务设置了密码）
- attachments: 附件文件（可选）

**响应**：

```json
{
  "message": "提交成功",
  "filename": "模板1-张三-中部_v1-20260115-150850.xlsx",
  "submitter": "张三",
  "contact": "1234"
}
```

### 在线填表接口

#### 获取 Schema

GET /api/distribution/{slug}/schema

**响应**：

```json
{
  "title": "测试在线填表分发描述",
  "columns": [
    {
      "name": "遗失数量",
      "type": "数字",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "回收数量",
      "type": "数字",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "已申报",
      "type": "双选框(是/否)",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    },
    {
      "name": "已处理",
      "type": "双选框(是/否)",
      "required": true,
      "description": null,
      "mergeMode": 0,
      "groupByField": null
    }
  ],
  "allowAttachmentUpload": true
}
```

#### 提交表单

POST /api/distribution/{slug}/submit
Content-Type: multipart/form-data

**请求体**：

```body
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="name"

张三
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="contact"

1234
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="department"

中部
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="jsonData"

{"遗失数量":12,"回收数量":13,"已申报":"true","已处理":"true"}
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="password"

123
------WebKitFormBoundaryhkl9gh3ycY9A95Gs
Content-Disposition: form-data; name="attachment"; filename="模拟人员列表.xlsx"
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet


------WebKitFormBoundaryhkl9gh3ycY9A95Gs--
```

**响应**：

```json
{
  "message": "提交成功",
  "filename": "张三_中部_20260115-151246.json",
  "submitter": "张三",
  "contact": "1234",
  "department": "中部",
  "attachmentCount": 1
}
```

#### 获取附件列表

GET /api/distribution/{slug}/attachments

**响应**：

```json
{
  "attachments": [
    {
      "id": 3,
      "fileName": "超超超超超超超超超超超超超超超超超超超超超超超超超超超长文件名-json-test合并结果-累计模式 - 副本.xlsx",
      "displayName": "超超超超超超超超超超超超超超超超超超超超超超超超超超超长文件名-json-test合并结果-累计模式 - 副本",
      "fileSize": 10590,
      "formattedFileSize": "10.34 KB",
      "uploadDate": "2026-01-15T15:11:32.3865176",
      "sortOrder": 0
    }
  ],
  "attachmentDownloadDescription": "这是附件的下载提示...."
}
```

#### 下载任务附件

GET /api/distribution/{slug}/attachments/{attachmentId}

**响应**：

- 成功：返回附件文件
- 失败：返回错误信息

### 管理接口

#### 部门管理

GET /api/department # 获取所有部门
POST /api/department # 添加部门
PUT /api/department/{id} # 更新部门
DELETE /api/department/{id} # 删除部门
POST /api/department/sort/{id} # 部门排序（上移/下移/置顶）
GET /api/department/export # 导出 Excel
POST /api/department/import # 导入 Excel

#### 人员管理

GET /api/person # 获取所有人员
POST /api/person # 添加人员
PUT /api/person/{id} # 更新人员
DELETE /api/person/{id} # 删除人员
GET /api/person/search # 搜索人员
GET /api/person/export # 导出 Excel
POST /api/person/import # 导入 Excel
DELETE /api/person/clear # 清空所有人员

### 错误码说明

| HTTP 状态码 | 错误类型  | 说明          |
|----------|-------|-------------|
| 200      | 成功    | 请求成功        |
| 400      | 错误的请求 | 参数错误、验证失败   |
| 404      | 未找到   | 任务不存在、文件不存在 |
| 409      | 冲突    | 重复提交（一次性限制） |
| 500      | 服务器错误 | 内部错误        |

**错误响应格式**：

```json
{
  "error": "错误类型",
  "message": "详细错误信息"
}
```

---

## 配置说明

### 服务器配置

**端口设置**：

- 默认端口：8080
- 修改方式：主界面端口输入框
- 绑定地址：0.0.0.0（所有网络接口）

**自动刷新**：

- 启用后自动刷新任务列表
- 可配置刷新间隔（秒）
- 状态栏显示刷新状态

### 过期时间快捷选项

**配置文件**：config/expiry_quick_options.json

**默认选项**：

```json
{
  "Options": [
    {
      "DisplayName": "1小时",
      "Hours": 1,
      "Days": null
    },
    {
      "DisplayName": "12小时",
      "Hours": 12,
      "Days": null
    },
    {
      "DisplayName": "1天",
      "Hours": null,
      "Days": 1
    },
    {
      "DisplayName": "3天",
      "Hours": null,
      "Days": 3
    },
    {
      "DisplayName": "7天",
      "Hours": null,
      "Days": 7
    },
    {
      "DisplayName": "15天",
      "Hours": null,
      "Days": 15
    },
    {
      "DisplayName": "30天",
      "Hours": null,
      "Days": 30
    },
    {
      "DisplayName": "60天",
      "Hours": null,
      "Days": 60
    },
    {
      "DisplayName": "90天",
      "Hours": null,
      "Days": 90
    }
  ],
  "LastUpdated": "2026-01-11T13:09:35.1396661Z"
}
```

**使用方式**：

- 右键任务  "延长过期时间"  选择快捷选项
- 支持累加：在原有过期时间基础上累加

### 文件扩展名白名单

**配置文件**：config/{任务标题}/task_extensions.json

**默认白名单**：

```json
{
  "allowedExtensions": [
    ".xlsx",
    ".xls",
    ".xlsm",
    ".pdf",
    ".doc",
    ".docx",
    ".docm",
    ".ppt",
    ".pptx",
    ".txt",
    ".csv",
    ".json",
    ".xml",
    ".jpg",
    ".jpeg",
    ".png",
    ".gif",
    ".bmp",
    ".zip",
    ".rar",
    ".7z",
    ".mp3",
    ".mp4",
    ".avi"
  ]
}
```

**用途**：

- 限制用户可上传的文件类型
- 用于附件上传验证
- 可按任务单独配置

---

## 安全特性

### 密码保护

**PBKDF2 哈希算法**：

- 迭代次数：10,000
- 哈希算法：SHA-256
- Salt 长度：128 位
- Hash 长度：256 位

### 文件类型验证

**Magic Number 验证**：

- 检查文件头魔数，防止文件类型伪装
- 拒绝可执行文件（.exe, .bat, .cmd, .com 等）

**支持的文件类型**：

- Office：.doc, .docx, .xls, .xlsx, .ppt, .pptx
- PDF：.pdf
- 图片：.jpg, .jpeg, .png, .gif, .bmp
- 文本：.txt, .csv, .json, .xml
- 压缩包：.zip, .rar, .7z
- 媒体：.mp3, .mp4, .avi

**文件大小限制**：

- 单文件：50MB
- 总请求大小：50MB

### 路径遍历防护

**路径清理**：

- 使用 SecurityHelper.SanitizePathSegment() 清理路径
- 移除 .., , :, 等危险字符
- 限制文件名长度（1-255 字符）

### 提交限制

**提交上限**：

- 限制总提交数量
- 达到上限后自动停止接收

**过期时间**：

- 任务过期后自动停止接收
- 支持快捷时间延长

## 常见问题

### 故障排查

**问题**：服务器无法启动

- **检查**：端口是否被占用（netstat -ano findstr 8080）
- **解决**：更换端口或关闭占用进程
- **检查**：防火墙设置
- **检查**：日志文件 logs/app-YYYYMMDD.log

**问题**：用户无法访问任务链接

- **检查**：服务器已启动
- **检查**：网络连接（ping 服务器 IP）
- **检查**：任务状态（是否激活、是否过期）
- **检查**：URL 格式是否正确（包含 Slug）

**问题**：文件合并失败

- **检查**：所有文件格式正确
- **检查**：表头行设置是否正确
- **检查**：去重字段是否存在于所有文件
- **解决**：查看合并日志，定位问题文件

**问题**：数据库错误

- **检查**：config/server_v1.db 文件是否存在
- **解决**：删除数据库文件，重新启动应用自动创建

### 性能优化

**数据库优化**：

- 已创建索引，加速查询
- WAL 模式支持读写并发
- 定期清理过期任务

**文件处理优化**：

- IO 锁防止并发冲突
- 流式处理大文件
- MiniExcel 高性能读取

**网络优化**：

- 静态文件缓存
- Gzip 压缩（可选）
- Keep-Alive 连接

### 使用技巧

1. **快速延长过期时间**
    - 右键任务  "延长过期时间"
    - 选择快捷时间选项（1h, 1d, 7d 等）

2. **批量创建相似任务**
    - 右键任务  "复刻分发"
    - 保留原设置，生成新 Slug

3. **版本管理**
    - 用户多次提交自动生成版本号
    - 合并时自动选择最新版本

4. **数据导出**
    - 部门/人员 导出 Excel
    - 在线填表 使用统计合并模式生成报表

5. **快速定位任务**
    - 使用搜索框（支持标题、描述搜索）
    - 搜索支持防抖（300ms）

6. **自动刷新**
    - 启用自动刷新，实时查看提交
    - 可配置刷新间隔

---

## 开发指南

### 代码结构

**关键文件路径**：

| 功能          | 文件路径                                                      |
|-------------|-----------------------------------------------------------|
| 主窗口         | UI/Views/MainWindow.xaml                                  |
| 主 ViewModel | UI/ViewModels/MainViewModel.cs                            |
| 任务仓储        | Infrastructure/Data/TaskRepository.cs                     |
| 提交服务        | Infrastructure/Services/SubmissionService.cs              |
| Excel 合并    | Infrastructure/Services/ExcelMergeService.cs              |
| 任务验证        | Infrastructure/Web/Middleware/TaskValidationMiddleware.cs |
| 上传控制器       | Infrastructure/Web/Controllers/UploadController.cs        |
| 在线填表控制器     | Infrastructure/Web/Controllers/DistributionController.cs  |

### 添加新功能

**1. 添加新的 API 接口**：

- 在 Infrastructure/Web/Controllers/ 创建控制器
- 注册路由和中间件
- 添加异常处理

**2. 添加新的数据模型**：

- 在 Core/Models/ 创建模型类
- 在 DapperContext 添加表创建逻辑
- 创建对应的 Repository

**3. 添加新的 UI 功能**：

- 在 UI/ViewModels/ 创建 ViewModel
- 在 UI/Views/ 创建 View
- 在 MainViewModel 添加命令

**4. 添加新的服务**：

- 在 Core/Interfaces/ 创建接口
- 在 Infrastructure/Services/ 实现服务
- 在 Program.cs 注册服务

**代码规范**：

- 遵循 C# 编码规范
- 使用 XML 注释文档
- 提交前运行代码格式化工具
- 添加必要的单元测试

---

## 更新日志

### 版本 1.0.0 (2025-01-11)

**初始版本**：

- 支持文件收集和在线填表两种模式
- Excel 智能合并（版本控制、去重、统计）
- 部门和人员管理
- 密码保护和提交限制
- 附件管理（任务附件、用户附件）
- Python 测试脚本
- 详细的中文文档
- 安全特性（魔数验证、路径防护、XSS 防护）

---

<div align="center">

**如果这个项目对您有帮助，请给一个 Star**

</div>
