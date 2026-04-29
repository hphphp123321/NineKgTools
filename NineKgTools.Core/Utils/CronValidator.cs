namespace NineKgTools.Utils;

/// <summary>
/// Cron 表达式验证器
/// 支持标准5段格式：分 时 日 月 周
/// </summary>
public static class CronValidator
{
    /// <summary>
    /// 各字段的有效范围
    /// </summary>
    private static readonly (int Min, int Max)[] FieldRanges =
    [
        (0, 59),  // 分钟
        (0, 23),  // 小时
        (1, 31),  // 日期
        (1, 12),  // 月份
        (0, 7)    // 星期（0和7都表示星期日）
    ];

    /// <summary>
    /// 字段名称（用于错误提示）
    /// </summary>
    private static readonly string[] FieldNames =
    [
        "分钟",
        "小时",
        "日期",
        "月份",
        "星期"
    ];

    /// <summary>
    /// 验证 Cron 表达式是否有效
    /// </summary>
    /// <param name="expression">Cron 表达式</param>
    /// <returns>是否有效</returns>
    public static bool IsValid(string? expression)
    {
        return Validate(expression).IsValid;
    }

    /// <summary>
    /// 验证 Cron 表达式并返回详细结果
    /// </summary>
    /// <param name="expression">Cron 表达式</param>
    /// <returns>验证结果，包含是否有效和错误信息</returns>
    public static CronValidationResult Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new CronValidationResult(false, "Cron 表达式不能为空");
        }

        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 5)
        {
            return new CronValidationResult(false, $"Cron 表达式应包含5个字段（分 时 日 月 周），当前有 {parts.Length} 个字段");
        }

        for (int i = 0; i < 5; i++)
        {
            var fieldResult = ValidateField(parts[i], FieldRanges[i].Min, FieldRanges[i].Max, FieldNames[i]);
            if (!fieldResult.IsValid)
            {
                return fieldResult;
            }
        }

        return new CronValidationResult(true);
    }

    /// <summary>
    /// 验证单个字段
    /// </summary>
    private static CronValidationResult ValidateField(string field, int min, int max, string fieldName)
    {
        // 处理逗号分隔的列表（如 1,3,5）
        var listParts = field.Split(',');

        foreach (var part in listParts)
        {
            var result = ValidateFieldPart(part.Trim(), min, max, fieldName);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return new CronValidationResult(true);
    }

    /// <summary>
    /// 验证字段的单个部分（处理范围和步进）
    /// </summary>
    private static CronValidationResult ValidateFieldPart(string part, int min, int max, string fieldName)
    {
        if (string.IsNullOrEmpty(part))
        {
            return new CronValidationResult(false, $"{fieldName}字段包含空值");
        }

        // 分离步进值（如 */5 或 1-10/2）
        var stepParts = part.Split('/');
        if (stepParts.Length > 2)
        {
            return new CronValidationResult(false, $"{fieldName}字段 '{part}' 格式错误：步进符号 / 使用不正确");
        }

        var basePart = stepParts[0];
        int? stepValue = null;

        // 验证步进值
        if (stepParts.Length == 2)
        {
            if (!int.TryParse(stepParts[1], out var step) || step <= 0)
            {
                return new CronValidationResult(false, $"{fieldName}字段 '{part}' 的步进值必须是正整数");
            }
            stepValue = step;
        }

        // 验证基础部分
        return ValidateBasePart(basePart, min, max, fieldName, stepValue);
    }

    /// <summary>
    /// 验证基础部分（* 或 数字 或 范围）
    /// </summary>
    private static CronValidationResult ValidateBasePart(string basePart, int min, int max, string fieldName, int? stepValue)
    {
        // 处理通配符 *
        if (basePart == "*")
        {
            // 验证步进值是否在合理范围内
            if (stepValue.HasValue && stepValue.Value > max - min + 1)
            {
                return new CronValidationResult(false,
                    $"{fieldName}字段的步进值 {stepValue} 超出有效范围（最大步进：{max - min + 1}）");
            }
            return new CronValidationResult(true);
        }

        // 处理范围（如 1-5）
        if (basePart.Contains('-'))
        {
            var rangeParts = basePart.Split('-');
            if (rangeParts.Length != 2)
            {
                return new CronValidationResult(false, $"{fieldName}字段 '{basePart}' 范围格式错误");
            }

            if (!int.TryParse(rangeParts[0], out var rangeStart))
            {
                return new CronValidationResult(false, $"{fieldName}字段范围起始值 '{rangeParts[0]}' 不是有效数字");
            }

            if (!int.TryParse(rangeParts[1], out var rangeEnd))
            {
                return new CronValidationResult(false, $"{fieldName}字段范围结束值 '{rangeParts[1]}' 不是有效数字");
            }

            if (rangeStart < min || rangeStart > max)
            {
                return new CronValidationResult(false,
                    $"{fieldName}字段范围起始值 {rangeStart} 超出有效范围（{min}-{max}）");
            }

            if (rangeEnd < min || rangeEnd > max)
            {
                return new CronValidationResult(false,
                    $"{fieldName}字段范围结束值 {rangeEnd} 超出有效范围（{min}-{max}）");
            }

            if (rangeStart > rangeEnd)
            {
                return new CronValidationResult(false,
                    $"{fieldName}字段范围起始值 {rangeStart} 不能大于结束值 {rangeEnd}");
            }

            return new CronValidationResult(true);
        }

        // 处理单个数字
        if (!int.TryParse(basePart, out var value))
        {
            return new CronValidationResult(false, $"{fieldName}字段值 '{basePart}' 不是有效数字");
        }

        if (value < min || value > max)
        {
            return new CronValidationResult(false,
                $"{fieldName}字段值 {value} 超出有效范围（{min}-{max}）");
        }

        return new CronValidationResult(true);
    }

    /// <summary>
    /// 获取 Cron 表达式的人类可读描述
    /// </summary>
    /// <param name="expression">Cron 表达式</param>
    /// <returns>描述文本</returns>
    public static string GetDescription(string? expression)
    {
        if (!IsValid(expression))
        {
            return "无效的 Cron 表达式";
        }

        // 常用表达式的预设描述
        return expression!.Trim() switch
        {
            "* * * * *" => "每分钟执行",
            "*/5 * * * *" => "每5分钟执行",
            "*/10 * * * *" => "每10分钟执行",
            "*/15 * * * *" => "每15分钟执行",
            "*/30 * * * *" => "每30分钟执行",
            "0 * * * *" => "每小时整点执行",
            "30 * * * *" => "每小时30分执行",
            "0 */2 * * *" => "每2小时执行",
            "0 */3 * * *" => "每3小时执行",
            "0 */4 * * *" => "每4小时执行",
            "0 */6 * * *" => "每6小时执行",
            "0 */8 * * *" => "每8小时执行",
            "0 */12 * * *" => "每12小时执行",
            "0 0 * * *" => "每天0点执行",
            "0 6 * * *" => "每天6点执行",
            "0 12 * * *" => "每天12点执行",
            "0 18 * * *" => "每天18点执行",
            "30 */6 * * *" => "每6小时的第30分钟执行",
            "0 0 * * 0" => "每周日0点执行",
            "0 0 * * 1" => "每周一0点执行",
            "0 0 1 * *" => "每月1日0点执行",
            "0 0 1,15 * *" => "每月1日和15日0点执行",
            _ => GenerateDescription(expression)
        };
    }

    /// <summary>
    /// 动态生成描述
    /// </summary>
    private static string GenerateDescription(string expression)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return "自定义执行计划";

        var minute = parts[0];
        var hour = parts[1];
        var day = parts[2];
        var month = parts[3];
        var weekday = parts[4];

        var descriptions = new List<string>();

        // 分析执行频率
        if (minute.StartsWith("*/"))
        {
            descriptions.Add($"每{minute[2..]}分钟");
        }
        else if (hour.StartsWith("*/"))
        {
            var minuteDesc = minute == "0" ? "整点" : $"第{minute}分";
            descriptions.Add($"每{hour[2..]}小时的{minuteDesc}");
        }
        else if (minute != "*" && hour != "*" && day == "*" && month == "*")
        {
            descriptions.Add($"每天{hour}:{minute.PadLeft(2, '0')}");
        }

        // 分析星期限制
        if (weekday != "*")
        {
            var weekdayDesc = GetWeekdayDescription(weekday);
            if (!string.IsNullOrEmpty(weekdayDesc))
            {
                descriptions.Add(weekdayDesc);
            }
        }

        // 分析日期限制
        if (day != "*")
        {
            descriptions.Add($"每月{day}日");
        }

        // 分析月份限制
        if (month != "*")
        {
            descriptions.Add($"{month}月");
        }

        return descriptions.Count > 0 ? string.Join("，", descriptions) + "执行" : "自定义执行计划";
    }

    /// <summary>
    /// 获取星期描述
    /// </summary>
    private static string GetWeekdayDescription(string weekday)
    {
        var weekdayNames = new[] { "日", "一", "二", "三", "四", "五", "六", "日" };

        if (int.TryParse(weekday, out var day) && day >= 0 && day <= 7)
        {
            return $"每周{weekdayNames[day]}";
        }

        if (weekday.Contains(','))
        {
            var days = weekday.Split(',')
                .Select(d => int.TryParse(d, out var n) && n >= 0 && n <= 7 ? weekdayNames[n] : d)
                .ToList();
            return $"每周{string.Join("、", days)}";
        }

        if (weekday.Contains('-'))
        {
            var range = weekday.Split('-');
            if (range.Length == 2 &&
                int.TryParse(range[0], out var start) &&
                int.TryParse(range[1], out var end) &&
                start >= 0 && start <= 7 && end >= 0 && end <= 7)
            {
                return $"每周{weekdayNames[start]}至周{weekdayNames[end]}";
            }
        }

        return "";
    }
}

/// <summary>
/// Cron 验证结果
/// </summary>
public readonly struct CronValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 错误信息（验证失败时）
    /// </summary>
    public string? ErrorMessage { get; }

    public CronValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 隐式转换为 bool
    /// </summary>
    public static implicit operator bool(CronValidationResult result) => result.IsValid;
}
