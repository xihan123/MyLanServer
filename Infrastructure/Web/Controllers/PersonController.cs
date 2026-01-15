using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Web.Controllers;

[ApiController]
[Route("api")]
public class PersonController : ControllerBase
{
    private readonly IIdCardValidationService _idCardValidationService;
    private readonly ILogger<PersonController> _logger;
    private readonly IPersonRepository _personRepository;

    public PersonController(
        IPersonRepository personRepository,
        IIdCardValidationService idCardValidationService,
        ILogger<PersonController> logger)
    {
        _personRepository = personRepository;
        _idCardValidationService = idCardValidationService;
        _logger = logger;
    }

    /// <summary>
    ///     获取所有人员列表
    /// </summary>
    [HttpGet("persons")]
    public async Task<IActionResult> GetAllPersons()
    {
        try
        {
            var persons = await _personRepository.GetAllAsync();
            return Ok(persons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有人员失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     根据 ID 获取单个人员
    /// </summary>
    [HttpGet("persons/{id}")]
    public async Task<IActionResult> GetPersonById(int id)
    {
        try
        {
            var person = await _personRepository.GetByIdAsync(id);
            if (person == null) return NotFound(new { error = "人员不存在" });

            return Ok(person);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取人员失败，ID: {PersonId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     创建新人员
    /// </summary>
    [HttpPost("persons")]
    public async Task<IActionResult> CreatePerson([FromBody] Person person)
    {
        try
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(person.Name)) return BadRequest(new { error = "姓名不能为空" });

            if (string.IsNullOrWhiteSpace(person.IdCard)) return BadRequest(new { error = "身份证号不能为空" });

            // 验证身份证号
            var validationError = _idCardValidationService.GetValidationError(person.IdCard);
            if (validationError != null) return BadRequest(new { error = $"身份证号验证失败：{validationError}" });

            // 自动计算年龄、性别、出生日期
            person.Age = _idCardValidationService.GetAge(person.IdCard);
            person.Gender = _idCardValidationService.GetGender(person.IdCard);
            person.BirthDate = _idCardValidationService.GetBirthDate(person.IdCard);

            var success = await _personRepository.CreateAsync(person);
            if (!success) return BadRequest(new { error = "身份证号已存在" });

            return Ok(new { message = "创建人员成功", person });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建人员失败: {Name}", person.Name);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     更新人员信息
    /// </summary>
    [HttpPut("persons/{id}")]
    public async Task<IActionResult> UpdatePerson(int id, [FromBody] Person person)
    {
        try
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(person.Name)) return BadRequest(new { error = "姓名不能为空" });

            if (string.IsNullOrWhiteSpace(person.IdCard)) return BadRequest(new { error = "身份证号不能为空" });

            // 验证身份证号
            var validationError = _idCardValidationService.GetValidationError(person.IdCard);
            if (validationError != null) return BadRequest(new { error = $"身份证号验证失败：{validationError}" });

            // 检查人员是否存在
            var existingPerson = await _personRepository.GetByIdAsync(id);
            if (existingPerson == null) return NotFound(new { error = "人员不存在" });

            // 自动计算年龄、性别、出生日期
            person.Age = _idCardValidationService.GetAge(person.IdCard);
            person.Gender = _idCardValidationService.GetGender(person.IdCard);
            person.BirthDate = _idCardValidationService.GetBirthDate(person.IdCard);
            person.Id = id;

            var success = await _personRepository.UpdateAsync(person);
            if (!success) return BadRequest(new { error = "身份证号已存在" });

            return Ok(new { message = "更新人员成功", person });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新人员失败，ID: {PersonId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     删除人员
    /// </summary>
    [HttpDelete("persons/{id}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        try
        {
            var success = await _personRepository.DeleteAsync(id);
            if (!success) return NotFound(new { error = "人员不存在" });

            return Ok(new { message = "删除人员成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除人员失败，ID: {PersonId}", id);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     模糊搜索人员
    /// </summary>
    /// <param name="q">搜索关键词</param>
    /// <param name="limit">返回结果数量限制（1-50）</param>
    /// <param name="fields">指定搜索字段（逗号分隔，如：name,contact,employeeNumber），支持字段：name,idcard,contact,employeenumber,department,rank,registeredaddress</param>
    [HttpGet("persons/search")]
    public async Task<IActionResult> SearchPersons([FromQuery] string q, [FromQuery] int limit = 10,
        [FromQuery] string? fields = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "搜索关键词不能为空" });

            if (q.Length < 1 || q.Length > 50) return BadRequest(new { error = "搜索关键词长度必须在 1-50 位之间" });

            if (limit < 1 || limit > 50) limit = 10;

            var results = await _personRepository.SearchAsync(q, limit, fields);
            var resultList = results.ToList();

            return Ok(new { results = resultList, count = resultList.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索人员失败: {Keyword}", q);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     导入人员列表（接收 Excel 文件）
    /// </summary>
    [HttpPost("persons/import")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> ImportPersons()
    {
        try
        {
            var form = await Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();

            if (file == null || file.Length == 0) return BadRequest(new { error = "请上传 Excel 文件" });

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (fileExtension != ".xlsx" && fileExtension != ".xls")
                return BadRequest(new { error = "仅支持 .xlsx 或 .xls 格式的 Excel 文件" });

            // 保存临时文件
            var tempFilePath = Path.GetTempFileName();
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var importedCount = await _personRepository.ImportFromExcelAsync(tempFilePath);
                return Ok(new { message = $"成功导入 {importedCount} 个人员", count = importedCount });
            }
            finally
            {
                // 删除临时文件
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入人员失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     导出人员列表（返回 Excel 文件）
    /// </summary>
    [HttpGet("persons/export")]
    public async Task<IActionResult> ExportPersons()
    {
        try
        {
            var tempFilePath = Path.GetTempFileName() + ".xlsx";

            try
            {
                var success = await _personRepository.ExportToExcelAsync(tempFilePath);
                if (!success) return BadRequest(new { error = "导出失败，可能没有人员数据" });

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"人员列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            finally
            {
                // 删除临时文件
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出人员失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     清空所有人员
    /// </summary>
    [HttpDelete("persons/clear")]
    public async Task<IActionResult> ClearAllPersons()
    {
        try
        {
            var success = await _personRepository.ClearAllAsync();
            if (!success) return BadRequest(new { error = "清空失败" });

            return Ok(new { message = "清空所有人员成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空所有人员失败");
            return StatusCode(500, new { error = "服务器错误" });
        }
    }
}