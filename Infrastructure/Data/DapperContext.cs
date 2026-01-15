using System.Data;
using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MyLanServer.Infrastructure.Data;

public class DapperContext
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly ILogger<DapperContext> _logger;

    public DapperContext(ILogger<DapperContext> logger)
    {
        _logger = logger;

        // 将数据库存放在应用程序目录的config文件夹下
        var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        _dbPath = Path.Combine(folder, "server_v1.db");

        // WAL 模式对并发至关重要；Cache=Shared 允许连接池复用缓存
        _connectionString = $"Data Source={_dbPath};Cache=Shared;Mode=ReadWriteCreate";
    }

    public IDbConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // 每次打开连接时执行 Pragma，确保性能配置生效
        // WAL (Write-Ahead Logging): 允许读写并发
        // NORMAL: 降低 sync 频率，提升 HDD 性能，虽然牺牲极小概率的数据安全性，但在局域网文件服务场景是值得的
        conn.Execute("PRAGMA journal_mode=WAL;");
        conn.Execute("PRAGMA synchronous=NORMAL;");

        return conn;
    }

    public async Task InitDatabaseAsync()
    {
        try
        {
            using var conn = CreateConnection();

            var sql = @"
                    CREATE TABLE IF NOT EXISTS Tasks (
                        Id TEXT PRIMARY KEY,
                        TemplatePath TEXT NOT NULL,
                        TargetFolder TEXT NOT NULL,
                        Slug TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT,
                        MaxLimit INTEGER,
                        CurrentCount INTEGER DEFAULT 0,
                        ExpiryDate TEXT,
                        IsActive INTEGER DEFAULT 1,
                        VersioningMode INTEGER DEFAULT 1,
                        DownloadsCount INTEGER DEFAULT 0,
                        IsOneTimeLink INTEGER DEFAULT 0,
                        Description TEXT,
                        TaskType INTEGER DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Submissions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TaskId TEXT NOT NULL,
                        SubmitterName TEXT,
                        Contact TEXT,
                        Department TEXT,
                        OriginalFilename TEXT,
                        StoredFilename TEXT,
                        Timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
                        ClientIP TEXT,
                        FOREIGN KEY(TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS TaskAttachments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TaskId TEXT NOT NULL,
                        FileName TEXT NOT NULL,
                        FilePath TEXT NOT NULL,
                        DisplayName TEXT,
                        Description TEXT,
                        FileSize INTEGER,
                        UploadDate TEXT DEFAULT CURRENT_TIMESTAMP,
                        SortOrder INTEGER DEFAULT 0,
                        FOREIGN KEY(TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS Departments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Persons (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        IdCard TEXT NOT NULL UNIQUE,
                        Contact1 TEXT,
                        Contact2 TEXT,
                        Department TEXT,
                        Age INTEGER,
                        Gender TEXT,
                        BirthDate TEXT,
                        RegisteredAddress TEXT,
                        CurrentAddress TEXT,
                        EmployeeNumber TEXT,
                        Rank1 TEXT,
                        Rank2 TEXT,
                        Position TEXT,
                        WorkStartDate TEXT,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE INDEX IF NOT EXISTS IDX_Task_Slug ON Tasks(Slug);
                    CREATE INDEX IF NOT EXISTS IDX_Sub_TaskId ON Submissions(TaskId);
                    CREATE INDEX IF NOT EXISTS IDX_TaskAttachment_TaskId ON TaskAttachments(TaskId);
                    CREATE INDEX IF NOT EXISTS IDX_TaskAttachment_SortOrder ON TaskAttachments(SortOrder);
                    CREATE INDEX IF NOT EXISTS IDX_Department_SortOrder ON Departments(SortOrder);
                    CREATE INDEX IF NOT EXISTS IDX_Person_IdCard ON Persons(IdCard);
                    CREATE INDEX IF NOT EXISTS IDX_Person_Name ON Persons(Name);
                    CREATE INDEX IF NOT EXISTS IDX_Person_Contact1 ON Persons(Contact1);
                    CREATE INDEX IF NOT EXISTS IDX_Person_Contact2 ON Persons(Contact2);
                    CREATE INDEX IF NOT EXISTS IDX_Person_Department ON Persons(Department);
                    CREATE INDEX IF NOT EXISTS IDX_Person_Position ON Persons(Position);
                    CREATE INDEX IF NOT EXISTS IDX_Person_EmployeeNumber ON Persons(EmployeeNumber);
                ";

            await conn.ExecuteAsync(sql);

            // 为现有数据库添加 TaskType 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN TaskType INTEGER DEFAULT 0");
                _logger.LogInformation("Added TaskType column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("TaskType column already exists in Tasks table");
            }

            // 更新现有任务的 NULL TaskType 值为默认值（FileCollection = 0）
            try
            {
                var updatedRows = await conn.ExecuteAsync(
                    "UPDATE Tasks SET TaskType = 0 WHERE TaskType IS NULL");
                if (updatedRows > 0)
                    _logger.LogInformation("Updated {Count} tasks with NULL TaskType to FileCollection", updatedRows);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update NULL TaskType values");
            }

            // 为现有数据库添加 Department 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Submissions ADD COLUMN Department TEXT");
                _logger.LogInformation("Added Department column to Submissions table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("Department column already exists in Submissions table");
            }

            // 为现有数据库添加 CreatedAt 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN CreatedAt TEXT");
                await conn.ExecuteAsync("UPDATE Tasks SET CreatedAt = CURRENT_TIMESTAMP WHERE CreatedAt IS NULL");
                _logger.LogInformation("Added CreatedAt column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("CreatedAt column already exists in Tasks table");
            }

            // 为现有数据库添加 AllowAttachmentUpload 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN AllowAttachmentUpload INTEGER DEFAULT 0");
                _logger.LogInformation("Added AllowAttachmentUpload column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("AllowAttachmentUpload column already exists in Tasks table");
            }

            // 为现有数据库添加 AttachmentDownloadDescription 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN AttachmentDownloadDescription TEXT");
                _logger.LogInformation("Added AttachmentDownloadDescription column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("AttachmentDownloadDescription column already exists in Tasks table");
            }

            // 为现有数据库添加 AttachmentPath 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Submissions ADD COLUMN AttachmentPath TEXT");
                _logger.LogInformation("Added AttachmentPath column to Submissions table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("AttachmentPath column already exists in Submissions table");
            }

            // 为现有数据库添加 Title 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN Title TEXT");
                _logger.LogInformation("Added Title column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("Title column already exists in Tasks table");
            }

            // 为现有数据库添加 ShowDescriptionInApi 列（如果不存在）
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Tasks ADD COLUMN ShowDescriptionInApi INTEGER DEFAULT 0");
                _logger.LogInformation("Added ShowDescriptionInApi column to Tasks table");
            }
            catch (SqliteException ex) when
                (ex.SqliteErrorCode == 1) // SQLite error code 1 = SQL error or missing database
            {
                // 列已存在，忽略错误
                _logger.LogInformation("ShowDescriptionInApi column already exists in Tasks table");
            }

            // 为 Departments 表添加 UNIQUE 约束（如果不存在）
            try
            {
                // 检查是否已经有 UNIQUE 约束（通过查询 sqlite_master 表）
                var tableSql = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT sql FROM sqlite_master WHERE type='table' AND name='Departments'");

                var hasUniqueConstraint = !string.IsNullOrEmpty(tableSql) && tableSql.Contains("UNIQUE");

                if (!hasUniqueConstraint)
                {
                    _logger.LogInformation("开始迁移 Departments 表，添加 Name 字段的 UNIQUE 约束");

                    // 使用事务确保迁移的原子性
                    using var trans = conn.BeginTransaction();
                    try
                    {
                        // 创建新表（带 UNIQUE 约束）
                        await conn.ExecuteAsync(@"
                            CREATE TABLE Departments_New (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Name TEXT NOT NULL UNIQUE,
                                SortOrder INTEGER NOT NULL DEFAULT 0
                            )
                        ", transaction: trans);

                        // 复制数据
                        await conn.ExecuteAsync(@"
                            INSERT INTO Departments_New (Id, Name, SortOrder)
                            SELECT Id, Name, SortOrder FROM Departments
                        ", transaction: trans);

                        // 删除旧表
                        await conn.ExecuteAsync("DROP TABLE Departments", transaction: trans);

                        // 重命名新表
                        await conn.ExecuteAsync("ALTER TABLE Departments_New RENAME TO Departments",
                            transaction: trans);

                        // 重建索引
                        await conn.ExecuteAsync(
                            "CREATE INDEX IF NOT EXISTS IDX_Department_SortOrder ON Departments(SortOrder)",
                            transaction: trans);

                        trans.Commit();
                        _logger.LogInformation("Departments 表迁移完成，已添加 UNIQUE 约束");
                    }
                    catch
                    {
                        trans.Rollback();
                        _logger.LogError("Departments 表迁移失败，已回滚事务");
                        throw;
                    }
                }
                else
                {
                    _logger.LogInformation("Departments 表已存在 UNIQUE 约束，跳过迁移");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "迁移 Departments 表时出现错误");
            }

            // 为 Persons 表添加新列（Contact1, Contact2, CurrentAddress, Rank1, Rank2, Position）
            try
            {
                // 添加 Contact1 列，并将原 Contact 数据迁移到 Contact1
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN Contact1 TEXT");
                await conn.ExecuteAsync("UPDATE Persons SET Contact1 = Contact WHERE Contact1 IS NULL");
                _logger.LogInformation("Added Contact1 column to Persons table and migrated data from Contact");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Contact1 column already exists in Persons table");
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN Contact2 TEXT");
                _logger.LogInformation("Added Contact2 column to Persons table");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Contact2 column already exists in Persons table");
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN CurrentAddress TEXT");
                _logger.LogInformation("Added CurrentAddress column to Persons table");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("CurrentAddress column already exists in Persons table");
            }

            try
            {
                // 添加 Rank1 列，并将原 Rank 数据迁移到 Rank1
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN Rank1 TEXT");
                await conn.ExecuteAsync("UPDATE Persons SET Rank1 = Rank WHERE Rank1 IS NULL");
                _logger.LogInformation("Added Rank1 column to Persons table and migrated data from Rank");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Rank1 column already exists in Persons table");
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN Rank2 TEXT");
                _logger.LogInformation("Added Rank2 column to Persons table");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Rank2 column already exists in Persons table");
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN Position TEXT");
                _logger.LogInformation("Added Position column to Persons table");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Position column already exists in Persons table");
            }

            // 添加 WorkStartDate 列
            try
            {
                await conn.ExecuteAsync("ALTER TABLE Persons ADD COLUMN WorkStartDate TEXT");
                _logger.LogInformation("Added WorkStartDate column to Persons table");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("WorkStartDate column already exists in Persons table");
            }

            // 为 WorkStartDate 列创建索引（如果索引不存在）
            try
            {
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS IDX_Person_WorkStartDate ON Persons(WorkStartDate)");
                _logger.LogInformation("Created index IDX_Person_WorkStartDate");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                _logger.LogInformation("Index IDX_Person_WorkStartDate already exists");
            }

            // 更新索引：删除旧的 Contact 索引，创建新的 Contact1 索引
            try
            {
                await conn.ExecuteAsync("DROP INDEX IF EXISTS IDX_Person_Contact");
                _logger.LogInformation("Dropped old IDX_Person_Contact index");
            }
            catch
            {
                // 索引不存在或已删除，忽略错误
            }

            _logger.LogInformation("Database initialized at {Path}", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize database.");
            throw;
        }
    }
}