using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Services;

public class ExcelMergeService : IExcelMergeService
{
    // 文件大小限制常量
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB
    private const long MaxTotalSize = 500 * 1024 * 1024; // 500MB
    private readonly ILogger<ExcelMergeService> _logger;

    public ExcelMergeService(ILogger<ExcelMergeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     将指定文件夹内的所有 Excel 文件合并为一个（支持版本选择和多字段去重）
    /// </summary>
    public async Task<MergeResult> MergeWithLatestVersionAsync(
        string sourceFolder,
        string outputPath,
        bool removeDuplicates,
        List<string> deduplicateColumns,
        string separator = "|",
        string? templatePath = null,
        int headerRowIndex = 0)
    {
        var result = new MergeResult();
        string? tempFile = null;

        try
        {
            if (!Directory.Exists(sourceFolder))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"源文件夹不存在: {sourceFolder}";
                return result;
            }

            var allFiles = Directory.GetFiles(sourceFolder, "*.xlsx");
            result.TotalFiles = allFiles.Length;

            // 选择最新版本的文件
            var latestFiles = SelectLatestVersionFiles(sourceFolder);
            result.FilteredFiles = allFiles.Length - latestFiles.Length;
            result.MergedFiles = latestFiles.Length;

            _logger.LogInformation(
                "Starting merge with latest version selection: {Count} files",
                latestFiles.Length);

            // 读取模板表头（如果提供了模板）
            List<string>? templateHeaders = null;
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                var fileExtension = Path.GetExtension(templatePath).ToLowerInvariant();

                if (fileExtension == ".json")
                {
                    // JSON 模板文件不应在 Excel 合并方法中处理
                    _logger.LogWarning(
                        "JSON template file provided to Excel merge method. Skipping template reading. Path: {Path}",
                        templatePath);
                }
                else
                {
                    // Excel 模板文件：使用自定义表头行读取
                    templateHeaders = ReadExcelHeaders(templatePath, headerRowIndex);
                    if (templateHeaders != null)
                        _logger.LogInformation("Template headers loaded from row {Index}: {Headers}",
                            headerRowIndex, string.Join(", ", templateHeaders));
                }
            }

            // 合并文件
            tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".xlsx");

            if (removeDuplicates && deduplicateColumns.Any())
            {
                // 多字段去重 - 使用新的方法，支持模板
                var stats = await MergeWithDeduplicationAsync(tempFile, latestFiles,
                    deduplicateColumns, separator, templateHeaders, headerRowIndex);
                result.TotalRecords = stats.TotalRecords;
                result.DeduplicatedRecords = stats.DeduplicatedRecords;
                result.DuplicatedCount = stats.DuplicatedCount;
            }
            else
            {
                // 普通合并（不去重）- 支持模板
                var totalRecords =
                    await MergeWithoutDeduplicationAsync(tempFile, latestFiles, templateHeaders, headerRowIndex);
                result.TotalRecords = totalRecords;
                result.DeduplicatedRecords = totalRecords;
                result.DuplicatedCount = 0;
            }

            // 替换输出文件
            if (File.Exists(outputPath)) File.Delete(outputPath);
            File.Move(tempFile, outputPath);

            result.OutputPath = outputPath;
            result.IsSuccess = true;

            _logger.LogInformation("Merge with latest version complete. Output: {Output}", outputPath);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merge with latest version failed");

            // 清理临时文件
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    ///     合并 JSON 文件并生成统计报表
    /// </summary>
    public async Task<MergeResult> MergeJsonFilesWithStatisticsAsync(
        string schemaPath,
        string sourceFolder,
        string outputPath,
        Dictionary<string, ColumnDefinition>? fieldMergeModes = null)
    {
        var result = new MergeResult();
        string? tempFile = null;

        try
        {
            if (!Directory.Exists(sourceFolder))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"源文件夹不存在: {sourceFolder}";
                return result;
            }

            if (!File.Exists(schemaPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"表格结构文件不存在: {schemaPath}";
                return result;
            }

            // 1. 读取 Schema 获取表头
            var schemaJson = await File.ReadAllTextAsync(schemaPath);
            var schema = JsonSerializer.Deserialize<TableSchema>(schemaJson);
            if (schema?.Columns == null || schema.Columns.Count == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "表格结构定义无效";
                return result;
            }

            // 2. 读取所有 JSON 文件（排除schema.json模板文件）
            var jsonFiles = Directory.GetFiles(sourceFolder, "*.json")
                .Where(f => !f.Equals(schemaPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            result.TotalFiles = jsonFiles.Length;

            if (jsonFiles.Length == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "没有找到提交的数据文件";
                return result;
            }

            _logger.LogInformation("Found {Count} JSON files to merge", jsonFiles.Length);

            // 3. 解析所有 JSON 数据
            var allData = new List<Dictionary<string, object?>>();
            foreach (var jsonFile in jsonFiles)
                try
                {
                    var jsonData = await File.ReadAllTextAsync(jsonFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonData);
                    if (data != null) allData.Add(data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析 JSON 文件失败: {File}", Path.GetFileName(jsonFile));
                }

            result.TotalRecords = allData.Count;

            if (allData.Count == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "没有有效的数据可以合并";
                return result;
            }

            // 4. 提取所有字段名（包括 schema 中定义的字段和数据中的额外字段）
            var allFieldNames = new HashSet<string>(schema.Columns.Select(c => c.Name));
            var excludedFields = new HashSet<string> { "title", "columns" };

            foreach (var data in allData)
            foreach (var key in data.Keys)
                // 排除元数据字段
                if (!string.IsNullOrEmpty(key) && !excludedFields.Contains(key.ToLower()))
                    allFieldNames.Add(key);

            _logger.LogInformation("Found {Count} fields to analyze: {Fields}",
                allFieldNames.Count, string.Join(", ", allFieldNames));

            // 5. 根据每个字段的 MergeMode 生成统计数据
            List<Dictionary<string, object?>> statisticsRows = new();

            foreach (var fieldName in allFieldNames)
            {
                // 获取该字段的合并模式配置
                var columnMergeMode = fieldMergeModes?.GetValueOrDefault(fieldName);

                if (columnMergeMode == null)
                {
                    // 如果没有配置，从 schema 中查找
                    columnMergeMode = schema.Columns.FirstOrDefault(c => c.Name == fieldName);

                    if (columnMergeMode == null)
                        // 如果 schema 中也没有，创建一个默认配置（文本类型，累计模式）
                        columnMergeMode = new ColumnDefinition
                        {
                            Name = fieldName,
                            Type = "文本",
                            MergeMode = MergeMode.Accumulate,
                            GroupByField = "所属部门"
                        };
                }

                // 特殊处理：如果字段是分组字段本身，跳过分组统计
                if (columnMergeMode.MergeMode == MergeMode.GroupBy &&
                    columnMergeMode.Name == columnMergeMode.GroupByField)
                {
                    _logger.LogDebug("Skipping group statistics for group field itself: {Field}", fieldName);
                    columnMergeMode = new ColumnDefinition
                    {
                        Name = columnMergeMode.Name,
                        Type = columnMergeMode.Type,
                        MergeMode = MergeMode.Accumulate,
                        GroupByField = columnMergeMode.GroupByField
                    };
                }

                if (columnMergeMode.MergeMode == MergeMode.GroupBy)
                {
                    // 分组统计模式
                    var groupRows = GenerateGroupByStatistics(schema, allData, columnMergeMode);
                    statisticsRows.AddRange(groupRows);
                }
                else
                {
                    // 累计模式（默认）
                    var countRow = GenerateAccumulateStatistics(schema, allData, columnMergeMode);
                    statisticsRows.Add(countRow);
                }
            }

            result.MergedFiles = statisticsRows.Count;

            // 5. 使用 MiniExcel 生成 Excel
            tempFile = Path.ChangeExtension(Path.GetTempFileName(), ".xlsx");
            await MiniExcel.SaveAsAsync(tempFile, statisticsRows, overwriteFile: true);

            // 6. 原子性替换文件
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            File.Move(tempFile, outputPath);

            result.OutputPath = outputPath;
            result.IsSuccess = true;
            result.ErrorMessage = $"成功生成统计报表，共 {statisticsRows.Count} 条记录";

            _logger.LogInformation("Statistics merge complete: {Output}, Records: {Count}",
                outputPath, statisticsRows.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合并 JSON 文件并生成统计报表失败");

            // 清理临时文件
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);

            result.IsSuccess = false;
            result.ErrorMessage = $"合并失败: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    ///     解析文件名，提取文件信息（支持版本管理）
    /// </summary>
    private ExcelFileInfo? ParseFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // 新格式：模板名-姓名-联系方式_v1-20260102-231729
        var match = Regex.Match(fileName, @"^(.+)-(.+)-(.+)_v(\d+)-(\d{8})-(\d{6})$");
        if (!match.Success) return null;

        var templateName = match.Groups[1].Value;
        var name = match.Groups[2].Value;
        var contact = match.Groups[3].Value;
        var version = int.Parse(match.Groups[4].Value);
        var timestampStr = match.Groups[5].Value + match.Groups[6].Value;

        if (DateTime.TryParseExact(timestampStr, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var timestamp))
            return new ExcelFileInfo
            {
                FilePath = filePath,
                TemplateName = templateName,
                Name = name,
                Contact = contact,
                Version = version,
                Timestamp = timestamp
            };

        return null;
    }

    /// <summary>
    ///     从文件夹中选择最新版本的文件
    /// </summary>
    private string[] SelectLatestVersionFiles(string sourceFolder)
    {
        var files = Directory.GetFiles(sourceFolder, "*.xlsx");
        var fileInfos = new List<ExcelFileInfo>();

        // 解析所有文件名
        foreach (var file in files)
        {
            var info = ParseFileName(file);
            if (info != null)
                fileInfos.Add(info);
            else
                // 文件名格式不匹配，保留（可能是模板文件或其他格式）
                fileInfos.Add(new ExcelFileInfo
                {
                    FilePath = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Version = 1,
                    Timestamp = File.GetCreationTime(file)
                });
        }

        // 按姓名+联系方式分组
        var grouped = fileInfos.GroupBy(f => f.UniqueId);

        // 每组选择版本号最大且时间戳最新的文件
        var latestFiles = grouped.Select(g =>
                g.OrderByDescending(f => f.Version)
                    .ThenByDescending(f => f.Timestamp)
                    .First())
            .Select(f => f.FilePath)
            .ToArray();

        _logger.LogInformation(
            "Version filtering: {Total} files -> {Merged} (filtered {Filtered} old versions)",
            files.Length,
            latestFiles.Length,
            files.Length - latestFiles.Length);

        return latestFiles;
    }

    /// <summary>
    ///     执行带去重的合并
    /// </summary>
    private async Task<MergeStatistics> MergeWithDeduplicationAsync(
        string outputPath,
        string[] files,
        List<string> deduplicateColumns,
        string separator,
        List<string>? templateHeaders = null,
        int headerRowIndex = 0)
    {
        var stats = new MergeStatistics();
        var processedKeys = new HashSet<string>();
        var allRows = new List<dynamic>();

        _logger.LogInformation("Starting deduplication with columns: {Columns}",
            string.Join(", ", deduplicateColumns));

        if (templateHeaders != null && templateHeaders.Any())
            _logger.LogInformation("Using template columns: {Headers}",
                string.Join(", ", templateHeaders));

        foreach (var file in files)
        {
            Stream? stream = null;
            try
            {
                stream = File.OpenRead(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Skipping file: {File}. Error: {Msg}",
                    Path.GetFileName(file), ex.Message);
                continue;
            }

            using (stream)
            {
                // 使用自定义表头行读取
                List<Dictionary<string, object?>> fileRows;
                try
                {
                    fileRows = ReadExcelWithCustomHeader(stream, headerRowIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file {File}", Path.GetFileName(file));
                    continue;
                }

                if (fileRows.Count == 0)
                {
                    _logger.LogWarning("File {File} has no data rows", Path.GetFileName(file));
                    continue;
                }

                // 从第一行获取表头
                var headers = fileRows[0].Keys.ToList();

                _logger.LogInformation("File {File}: Headers = {Headers}, Rows = {RowCount}",
                    Path.GetFileName(file),
                    string.Join(", ", headers),
                    fileRows.Count);

                // 如果有模板，使用模板表头；否则使用源文件表头
                var outputHeaders = templateHeaders != null && templateHeaders.Any() ? templateHeaders : headers;

                // 建立源文件到输出表头的列映射（不区分大小写）
                var columnMapping = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var outputHeader in outputHeaders)
                {
                    // 尝试在源文件中找到匹配的列名
                    var matchedHeader = headers.FirstOrDefault(h =>
                        h.Equals(outputHeader, StringComparison.OrdinalIgnoreCase));
                    columnMapping[outputHeader] = matchedHeader;
                }

                // 确定实际可用于去重的列（根据输出表头过滤）
                var availableDedupColumns = outputHeaders
                    .Where(col => deduplicateColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                // 检查哪些去重列不在输出表头中
                if (availableDedupColumns.Count != deduplicateColumns.Count)
                {
                    var missingInOutput = deduplicateColumns
                        .Where(col => !availableDedupColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    _logger.LogWarning(
                        "Dedup columns not in output headers: {Missing}. Available: {Available}. Using only matching columns.",
                        string.Join(", ", missingInOutput),
                        string.Join(", ", availableDedupColumns));
                }

                _logger.LogDebug("File {File}: Using {Count} columns for deduplication: {Columns}",
                    Path.GetFileName(file),
                    availableDedupColumns.Count,
                    string.Join(", ", availableDedupColumns));

                // 如果没有可用的去重列，则不做去重
                if (!availableDedupColumns.Any())
                {
                    _logger.LogWarning(
                        "File {File} has no matching dedup columns. Adding all rows without deduplication.",
                        Path.GetFileName(file));
                    allRows.AddRange(fileRows);
                    stats.TotalRecords += fileRows.Count;
                    continue;
                }

                // 统计总记录数
                stats.TotalRecords += fileRows.Count;

                // 处理所有行
                var fileProcessedCount = 0;
                var fileSkippedCount = 0;

                foreach (var row in fileRows)
                {
                    var rowDict = (IDictionary<string, object>)row;

                    // 构建组合键（使用输出表头的列）
                    var keyParts = availableDedupColumns.Select(col =>
                    {
                        // 在映射中查找源文件的列名
                        if (columnMapping.TryGetValue(col, out var sourceCol) && !string.IsNullOrEmpty(sourceCol))
                            return rowDict.TryGetValue(sourceCol, out var val) ? val?.ToString() ?? "" : "";

                        return "";
                    }).ToList();

                    var key = string.Join(separator, keyParts);

                    // 如果key为空，记录警告并跳过
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        fileSkippedCount++;
                        _logger.LogDebug(
                            "Skipping row with empty key from file {File}",
                            Path.GetFileName(file));
                        continue;
                    }

                    if (!processedKeys.Contains(key))
                    {
                        processedKeys.Add(key);
                        fileProcessedCount++;

                        // 创建新行，使用输出表头的列顺序和列名
                        var newRow = new Dictionary<string, object?>();
                        foreach (var outputHeader in outputHeaders)
                        {
                            var sourceCol = columnMapping[outputHeader];
                            if (!string.IsNullOrEmpty(sourceCol) && rowDict.ContainsKey(sourceCol))
                                newRow[outputHeader] = rowDict[sourceCol];
                            else
                                newRow[outputHeader] = null; // 列不存在时填充null
                        }

                        allRows.Add(newRow);
                        _logger.LogDebug("Added record with key: {Key}", key);
                    }
                    else
                    {
                        fileSkippedCount++;
                        _logger.LogDebug("Skipped duplicate record with key: {Key}", key);
                    }
                }

                _logger.LogInformation(
                    "File {File}: Processed {Processed} rows, Skipped {Skipped} rows",
                    Path.GetFileName(file),
                    fileProcessedCount,
                    fileSkippedCount);
            }
        }

        // 更新统计信息
        stats.DeduplicatedRecords = allRows.Count;
        stats.DuplicatedCount = stats.TotalRecords - stats.DeduplicatedRecords;

        _logger.LogInformation(
            "Deduplication complete: TotalRecords={Total}, Deduplicated={Dedup}, Duplicated={Dup}",
            stats.TotalRecords,
            stats.DeduplicatedRecords,
            stats.DuplicatedCount);

        // 保存到文件
        await MiniExcel.SaveAsAsync(outputPath, allRows, overwriteFile: true);

        return stats;
    }

    /// <summary>
    ///     执行不带去重的合并
    /// </summary>
    private async Task<int> MergeWithoutDeduplicationAsync(
        string outputPath,
        string[] files,
        List<string>? templateHeaders = null,
        int headerRowIndex = 0)
    {
        var allRows = new List<dynamic>();
        var outputHeaders = templateHeaders;

        foreach (var file in files)
        {
            Stream? stream = null;
            try
            {
                stream = File.OpenRead(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Skipping file: {File}. Error: {Msg}",
                    Path.GetFileName(file), ex.Message);
                continue;
            }

            using (stream)
            {
                // 使用自定义表头行读取
                List<Dictionary<string, object?>> fileRows;
                try
                {
                    fileRows = ReadExcelWithCustomHeader(stream, headerRowIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file {File}", Path.GetFileName(file));
                    continue;
                }

                if (fileRows.Count == 0)
                {
                    _logger.LogWarning("File {File} has no data rows", Path.GetFileName(file));
                    continue;
                }

                // 如果有模板，使用模板表头映射；否则直接添加
                if (templateHeaders != null && templateHeaders.Any())
                {
                    // 从第一行获取表头
                    var headers = fileRows[0].Keys.ToList();

                    // 建立源文件到输出表头的列映射（不区分大小写）
                    var columnMapping = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var outputHeader in templateHeaders)
                    {
                        var matchedHeader = headers.FirstOrDefault(h =>
                            h.Equals(outputHeader, StringComparison.OrdinalIgnoreCase));
                        columnMapping[outputHeader] = matchedHeader;
                    }

                    // 处理每一行，使用输出表头的列顺序
                    foreach (var row in fileRows)
                    {
                        var rowDict = (IDictionary<string, object>)row;
                        var newRow = new Dictionary<string, object?>();

                        foreach (var outputHeader in templateHeaders)
                        {
                            var sourceCol = columnMapping[outputHeader];
                            if (!string.IsNullOrEmpty(sourceCol) && rowDict.ContainsKey(sourceCol))
                                newRow[outputHeader] = rowDict[sourceCol];
                            else
                                newRow[outputHeader] = null; // 列不存在时填充null
                        }

                        allRows.Add(newRow);
                    }
                }
                else
                {
                    // 没有模板，直接添加所有行
                    allRows.AddRange(fileRows);
                }

                _logger.LogInformation("File {File}: Added {Count} rows", Path.GetFileName(file), fileRows.Count);
            }
        }

        _logger.LogInformation("Merge complete: TotalRecords={Total}", allRows.Count);

        // 保存到文件
        await MiniExcel.SaveAsAsync(outputPath, allRows, overwriteFile: true);

        return allRows.Count;
    }

    /// <summary>
    ///     生成累计模式统计：根据字段类型进行不同处理
    /// </summary>
    private Dictionary<string, object?> GenerateAccumulateStatistics(
        TableSchema schema,
        List<Dictionary<string, object?>> allData,
        ColumnDefinition column)
    {
        var row = new Dictionary<string, object?>
        {
            ["字段名称"] = column.Name,
            ["字段类型"] = column.Type,
            ["总提交数"] = allData.Count,
            ["详细信息"] = "" // 累计模式没有详细信息
        };

        // 根据字段类型进行不同处理
        if (column.Type == "数字")
        {
            // 数字类型：累加所有值
            double sum = 0;
            foreach (var data in allData)
                if (data.TryGetValue(column.Name, out var value))
                {
                    var valueStr = GetValueAsString(value);
                    if (IsNumber(valueStr)) sum += double.Parse(valueStr);
                }

            row["统计结果"] = sum.ToString("0.##"); // 保留两位小数
        }
        else if (column.Type == "双选框(是/否)")
        {
            // 双选框类型：统计是/否数量
            var yesCount = 0;
            var noCount = 0;

            foreach (var data in allData)
                if (data.TryGetValue(column.Name, out var value))
                {
                    var valueStr = GetValueAsString(value);
                    if (IsBoolean(valueStr))
                    {
                        var chineseValue = ConvertBooleanToChinese(valueStr);
                        if (chineseValue == "是")
                            yesCount++;
                        else if (chineseValue == "否")
                            noCount++;
                    }
                }

            row["统计结果"] = $"是（{yesCount}） 否（{noCount}）";
        }
        else
        {
            // 文本类型：全部列举所有值
            var values = new List<string>();
            foreach (var data in allData)
                if (data.TryGetValue(column.Name, out var value))
                {
                    var valueStr = GetValueAsString(value);
                    if (!string.IsNullOrEmpty(valueStr)) values.Add(valueStr);
                }

            // 去重并列举
            var uniqueValues = values.Distinct().ToList();

            // 特殊处理"所属部门"字段
            if (column.Name == "所属部门")
            {
                row["统计结果"] = $"共 {uniqueValues.Count} 个不同的部门";
                row["详细信息"] = string.Join("、", uniqueValues);
            }
            else
            {
                row["统计结果"] = string.Join("、", uniqueValues);
                row["详细信息"] = "";
            }
        }

        _logger.LogDebug("Accumulate statistics for {Column}: {Statistics}", column.Name, row["统计结果"]);

        return row;
    }

    /// <summary>
    ///     生成分组统计：按指定字段分组统计哪些部门/人员选择了哪些选项
    /// </summary>
    private List<Dictionary<string, object?>> GenerateGroupByStatistics(
        TableSchema schema,
        List<Dictionary<string, object?>> allData,
        ColumnDefinition column)
    {
        var rows = new List<Dictionary<string, object?>>();

        // 获取分组字段名称，如果没有指定则使用"所属部门"
        var groupByField = string.IsNullOrEmpty(column.GroupByField) ? "所属部门" : column.GroupByField;

        // 根据字段类型进行不同处理
        if (column.Type == "数字")
        {
            // 数字类型：累加每个分组中的值，为每个分组生成一行
            var groupSums = new Dictionary<string, double>();
            double totalSum = 0;

            foreach (var data in allData)
            {
                // 获取分组字段值
                var groupValue = GetValueAsString(data.GetValueOrDefault(groupByField));
                if (string.IsNullOrEmpty(groupValue)) groupValue = "未填写";

                // 获取该字段的值
                var valueStr = GetValueAsString(data.GetValueOrDefault(column.Name));
                if (IsNumber(valueStr))
                {
                    var value = double.Parse(valueStr);
                    totalSum += value;

                    if (!groupSums.ContainsKey(groupValue)) groupSums[groupValue] = 0;

                    groupSums[groupValue] += value;
                }
            }

            // 为每个分组生成一行统计结果
            foreach (var (group, sum) in groupSums)
            {
                var row = new Dictionary<string, object?>
                {
                    ["字段名称"] = column.Name,
                    ["字段类型"] = column.Type,
                    ["总提交数"] = allData.Count,
                    ["统计结果"] = sum.ToString("0.##"),
                    ["详细信息"] = $"{group}：{sum.ToString("0.##")}"
                };
                rows.Add(row);
            }
        }
        else if (column.Type == "双选框(是/否)")
        {
            // 双选框类型：统计每个分组中的是/否数量
            var groupStats = new Dictionary<string, Dictionary<string, int>>();

            foreach (var data in allData)
            {
                // 获取分组字段值
                var groupValue = GetValueAsString(data.GetValueOrDefault(groupByField));
                if (string.IsNullOrEmpty(groupValue)) groupValue = "未填写";

                // 获取该字段的值
                var valueStr = GetValueAsString(data.GetValueOrDefault(column.Name));
                if (IsBoolean(valueStr))
                {
                    var chineseValue = ConvertBooleanToChinese(valueStr);

                    if (!groupStats.ContainsKey(groupValue))
                        groupStats[groupValue] = new Dictionary<string, int>
                        {
                            ["是"] = 0,
                            ["否"] = 0
                        };

                    groupStats[groupValue][chineseValue]++;
                }
            }

            // 为每个分组生成一行统计结果
            foreach (var (group, stats) in groupStats)
            {
                var row = new Dictionary<string, object?>
                {
                    ["字段名称"] = column.Name,
                    ["字段类型"] = column.Type,
                    ["总提交数"] = allData.Count,
                    ["统计结果"] = $"是（{stats["是"]}） 否（{stats["否"]}）",
                    ["详细信息"] = $"{group}：是（{stats["是"]}人），否（{stats["否"]}人）"
                };
                rows.Add(row);
            }
        }
        else
        {
            // 文本类型：全部列举每个分组中的值（包括重复的）
            var groupValues = new Dictionary<string, List<string>>();

            foreach (var data in allData)
            {
                // 获取分组字段值
                var groupValue = GetValueAsString(data.GetValueOrDefault(groupByField));
                if (string.IsNullOrEmpty(groupValue)) groupValue = "未填写";

                // 获取该字段的值
                var valueStr = GetValueAsString(data.GetValueOrDefault(column.Name));
                if (!string.IsNullOrEmpty(valueStr))
                {
                    if (!groupValues.ContainsKey(groupValue)) groupValues[groupValue] = new List<string>();

                    groupValues[groupValue].Add(valueStr);
                }
            }

            // 为每个分组生成一行统计结果
            foreach (var (group, values) in groupValues)
            {
                // 显示所有值（包括重复的）
                var allValues = string.Join("、", values);
                var uniqueValues = values.Distinct().ToList();
                var uniqueValuesStr = string.Join("、", uniqueValues);

                var row = new Dictionary<string, object?>
                {
                    ["字段名称"] = column.Name,
                    ["字段类型"] = column.Type,
                    ["总提交数"] = allData.Count,
                    ["统计结果"] = uniqueValuesStr,
                    ["详细信息"] = $"{group}：{allValues}"
                };
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>
    ///     将值转换为字符串
    /// </summary>
    private string GetValueAsString(object? value)
    {
        if (value == null) return "";

        if (value is JsonElement element)
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.TryGetInt64(out var longVal)
                    ? longVal.ToString()
                    : element.GetDouble().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => element.ToString() ?? ""
            };

        return value.ToString() ?? "";
    }

    /// <summary>
    ///     判断字符串是否为数字
    /// </summary>
    private bool IsNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return double.TryParse(value, out _);
    }

    /// <summary>
    ///     判断字符串是否为布尔值
    /// </summary>
    private bool IsBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     将布尔值转换为中文
    /// </summary>
    private string ConvertBooleanToChinese(string value)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "是";
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "否";
        return value;
    }

    /// <summary>
    ///     从 Excel 文件中读取指定行的表头
    /// </summary>
    /// <param name="filePath">Excel 文件路径</param>
    /// <param name="headerRowIndex">表头行索引（从 0 开始）</param>
    /// <returns>表头列名列表，如果读取失败返回 null</returns>
    private List<string>? ReadExcelHeaders(string filePath, int headerRowIndex)
    {
        try
        {
            // 检查文件大小
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                _logger.LogError("File is empty: {File}", filePath);
                return null;
            }

            using var stream = File.OpenRead(filePath);

            // 使用 Query(false) 读取所有行（包括原始表头）
            var allRows = stream.Query().ToList();

            // 检查行索引是否有效
            if (headerRowIndex < 0 || headerRowIndex >= allRows.Count)
            {
                _logger.LogWarning(
                    "Header row index {Index} is out of range. Total rows: {Count}",
                    headerRowIndex, allRows.Count);
                return null;
            }

            // 获取表头行
            var headerRow = (IDictionary<string, object>)allRows[headerRowIndex];
            var headers = headerRow.Values
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                .Select(v => v!.ToString()!)
                .ToList();

            if (headers.Count == 0)
            {
                _logger.LogWarning("No valid headers found at row {Index}", headerRowIndex);
                return null;
            }

            return headers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read headers from {File} at row {Index}",
                filePath, headerRowIndex);
            return null;
        }
    }

    /// <summary>
    ///     使用自定义表头行读取 Excel 文件
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="headerRowIndex">表头行索引（从 0 开始）</param>
    /// <returns>数据行列表，每行是一个字典，键为表头列名</returns>
    private List<Dictionary<string, object?>> ReadExcelWithCustomHeader(
        Stream stream, int headerRowIndex)
    {
        var result = new List<Dictionary<string, object?>>();

        try
        {
            // 使用 Query(false) 读取所有行（包括原始表头）
            var allRows = stream.Query().ToList();

            // 检查行索引是否有效
            if (headerRowIndex < 0 || headerRowIndex >= allRows.Count)
            {
                _logger.LogWarning(
                    "Header row index {Index} is out of range. Total rows: {Count}",
                    headerRowIndex, allRows.Count);
                return result;
            }

            // 获取表头行
            var headerRow = (IDictionary<string, object>)allRows[headerRowIndex];
            var headers = headerRow.Values
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                .Select(v => v!.ToString()!)
                .ToList();

            if (headers.Count == 0)
            {
                _logger.LogWarning("No valid headers found at row {Index}", headerRowIndex);
                return result;
            }

            // 从 headerRowIndex + 1 开始读取数据行
            for (var i = headerRowIndex + 1; i < allRows.Count; i++)
            {
                var dataRow = (IDictionary<string, object>)allRows[i];
                var rowDict = new Dictionary<string, object?>();

                // 将数据行的值映射到表头列名
                var values = dataRow.Values.ToList();
                for (var j = 0; j < headers.Count && j < values.Count; j++) rowDict[headers[j]] = values[j];

                result.Add(rowDict);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Excel with custom header at row {Index}. Error: {Error}",
                headerRowIndex, ex.Message);
            return result;
        }
    }

    /// <summary>
    ///     合并统计信息
    /// </summary>
    private class MergeStatistics
    {
        public int TotalRecords { get; set; }
        public int DeduplicatedRecords { get; set; }
        public int DuplicatedCount { get; set; }
    }
}