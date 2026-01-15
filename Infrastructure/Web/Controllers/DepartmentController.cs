using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Web.Controllers;

/// <summary>
///     部门管理控制器 - 提供部门的 CRUD 操作
/// </summary>
[ApiController]
[Route("api")]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentController> _logger;

    public DepartmentController(
        IDepartmentRepository departmentRepository,
        ILogger<DepartmentController> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    /// <summary>
    ///     获取所有部门（按 SortOrder 排序）
    /// </summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetAllDepartments()
    {
        try
        {
            var departments = await _departmentRepository.GetAllAsync();
            return Ok(departments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有部门失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     根据 ID 获取单个部门
    /// </summary>
    [HttpGet("departments/{id}")]
    public async Task<IActionResult> GetDepartmentById(int id)
    {
        try
        {
            var department = await _departmentRepository.GetByIdAsync(id);
            if (department == null) return NotFound(new { error = "部门不存在" });

            return Ok(department);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取部门失败，ID: {DepartmentId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     创建新部门
    /// </summary>
    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] Department department)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(department.Name)) return BadRequest(new { error = "部门名称不能为空" });

            var success = await _departmentRepository.CreateAsync(department);
            if (!success) return BadRequest(new { error = "创建部门失败" });

            return Ok(new { message = "创建部门成功", department });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建部门失败: {DepartmentName}", department.Name);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     更新部门信息
    /// </summary>
    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] Department department)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(department.Name)) return BadRequest(new { error = "部门名称不能为空" });

            var existing = await _departmentRepository.GetByIdAsync(id);
            if (existing == null) return NotFound(new { error = "部门不存在" });

            department.Id = id;
            var success = await _departmentRepository.UpdateAsync(department);
            if (!success) return BadRequest(new { error = "更新部门失败" });

            return Ok(new { message = "更新部门成功", department });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新部门失败，ID: {DepartmentId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     删除部门（硬删除，不影响历史提交记录）
    /// </summary>
    [HttpDelete("departments/{id}")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        try
        {
            var existing = await _departmentRepository.GetByIdAsync(id);
            if (existing == null) return NotFound(new { error = "部门不存在" });

            var success = await _departmentRepository.DeleteAsync(id);
            if (!success) return BadRequest(new { error = "删除部门失败" });

            return Ok(new { message = "删除部门成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除部门失败，ID: {DepartmentId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     上移部门
    /// </summary>
    [HttpPost("departments/{id}/moveup")]
    public async Task<IActionResult> MoveUpDepartment(int id)
    {
        try
        {
            var success = await _departmentRepository.MoveUpAsync(id);
            if (!success) return BadRequest(new { error = "上移失败，可能已经是第一个部门" });

            return Ok(new { message = "上移成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上移部门失败，ID: {DepartmentId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     下移部门
    /// </summary>
    [HttpPost("departments/{id}/movedown")]
    public async Task<IActionResult> MoveDownDepartment(int id)
    {
        try
        {
            var success = await _departmentRepository.MoveDownAsync(id);
            if (!success) return BadRequest(new { error = "下移失败，可能已经是最后一个部门" });

            return Ok(new { message = "下移成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下移部门失败，ID: {DepartmentId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     导入部门列表（接收 Excel 文件）
    /// </summary>
    [HttpPost("departments/import")]
    public async Task<IActionResult> ImportDepartments()
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return BadRequest(new { error = "请上传 Excel 文件" });

            // 验证文件类型
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension)) return BadRequest(new { error = "只支持 .xlsx 或 .xls 格式的文件" });

            // 保存临时文件
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"departments_import_{Guid.NewGuid()}{fileExtension}");
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var importedCount = await _departmentRepository.ImportFromExcelAsync(tempFilePath);
                return Ok(new { message = $"成功导入 {importedCount} 个部门", count = importedCount });
            }
            finally
            {
                // 删除临时文件
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入部门失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     导出部门列表（返回 Excel 文件）
    /// </summary>
    [HttpGet("departments/export")]
    public async Task<IActionResult> ExportDepartments()
    {
        try
        {
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"departments_export_{Guid.NewGuid()}.xlsx");

            try
            {
                var success = await _departmentRepository.ExportToExcelAsync(tempFilePath);
                if (!success) return BadRequest(new { error = "导出失败，可能没有部门数据" });

                // 读取文件并返回
                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "部门列表.xlsx");
            }
            finally
            {
                // 删除临时文件
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出部门失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }
}