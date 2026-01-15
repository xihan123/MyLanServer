using CommunityToolkit.Mvvm.ComponentModel;

namespace MyLanServer.Core.Models;

/// <summary>
///     代表一个人员
/// </summary>
public partial class Person : ObservableObject
{
    /// <summary>
    ///     年龄（自动计算）
    /// </summary>
    [ObservableProperty] private int? _age;

    /// <summary>
    ///     出生日期（自动计算）
    /// </summary>
    [ObservableProperty] private DateTime? _birthDate;

    /// <summary>
    ///     联系方式1
    /// </summary>
    [ObservableProperty] private string? _contact1;

    /// <summary>
    ///     联系方式2
    /// </summary>
    [ObservableProperty] private string? _contact2;

    /// <summary>
    ///     创建时间
    /// </summary>
    [ObservableProperty] private DateTime _createdAt = DateTime.UtcNow;

    /// <summary>
    ///     现住址
    /// </summary>
    [ObservableProperty] private string? _currentAddress;

    /// <summary>
    ///     所属部门
    /// </summary>
    [ObservableProperty] private string? _department;

    /// <summary>
    ///     工号
    /// </summary>
    [ObservableProperty] private string? _employeeNumber;

    /// <summary>
    ///     性别（男/女，自动计算）
    /// </summary>
    [ObservableProperty] private string? _gender;

    /// <summary>
    ///     人员唯一标识（自增主键）
    /// </summary>
    [ObservableProperty] private int _id;

    /// <summary>
    ///     身份证号
    /// </summary>
    [ObservableProperty] private string _idCard = string.Empty;

    /// <summary>
    ///     姓名
    /// </summary>
    [ObservableProperty] private string _name = string.Empty;

    /// <summary>
    ///     职务
    /// </summary>
    [ObservableProperty] private string? _position;

    /// <summary>
    ///     职级1
    /// </summary>
    [ObservableProperty] private string? _rank1;

    /// <summary>
    ///     职级2
    /// </summary>
    [ObservableProperty] private string? _rank2;

    /// <summary>
    ///     户籍地址
    /// </summary>
    [ObservableProperty] private string? _registeredAddress;

    /// <summary>
    ///     更新时间
    /// </summary>
    [ObservableProperty] private DateTime _updatedAt = DateTime.UtcNow;

    /// <summary>
    ///     参与工作时间
    /// </summary>
    [ObservableProperty] private DateTime? _workStartDate;
}