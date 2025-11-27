using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.RegularExpressions;

namespace ASG.Api.Filters
{
    public class ModelStateChineseFilter : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is BadRequestObjectResult badRequest)
            {
                var errors = ExtractErrors(badRequest.Value);
                if (errors != null)
                {
                    var payload = new
                    {
                        error = new
                        {
                            message = "请求参数验证失败",
                            details = errors,
                            timestamp = DateTime.UtcNow
                        }
                    };
                    context.Result = new JsonResult(payload) { StatusCode = 400, ContentType = "application/json" };
                }
            }
        }

        public void OnResultExecuted(ResultExecutedContext context) { }

        private static Dictionary<string, string[]>? ExtractErrors(object? value)
        {
            if (value == null) return null;
            if (value is SerializableError se)
            {
                var dict = new Dictionary<string, string[]>();
                foreach (var kv in se)
                {
                    if (kv.Value is string[] arr)
                    {
                        dict[kv.Key] = arr.Select(Translate).ToArray();
                    }
                    else if (kv.Value is string s)
                    {
                        dict[kv.Key] = new[] { Translate(s) };
                    }
                    else
                    {
                        dict[kv.Key] = new[] { Translate(kv.Value?.ToString() ?? string.Empty) };
                    }
                }
                return dict;
            }
            if (value is ModelStateDictionary ms)
            {
                return ms.Where(x => x.Value.Errors.Count > 0)
                         .ToDictionary(
                             kvp => kvp.Key,
                             kvp => kvp.Value.Errors.Select(e => Translate(e.ErrorMessage ?? string.Empty)).ToArray());
            }
            if (value is ValidationProblemDetails vpd)
            {
                return vpd.Errors.ToDictionary(k => k.Key, v => v.Value.Select(Translate).ToArray());
            }
            return new Dictionary<string, string[]> { { "_", new[] { Translate(value.ToString() ?? string.Empty) } } };
        }

        private static string Translate(string msg)
        {
            var m = msg ?? string.Empty;
            m = Regex.Replace(m, "^The field (.+) is required\\.$", "字段 $1 为必填项");
            m = Regex.Replace(m, "^The value '(.+)' is not valid\\.$", "值 '$1' 无效");
            m = Regex.Replace(m, "^The field (.+) must be a string with a maximum length of (\\d+)\\.$", "字段 $1 的最大长度为 $2");
            m = Regex.Replace(m, "^The field (.+) must be a string with a minimum length of (\\d+)\\.$", "字段 $1 的最小长度为 $2");
            m = Regex.Replace(m, "^The field (.+) must be between (\\d+) and (\\d+)\\.$", "字段 $1 的取值范围为 $2 到 $3");
            m = Regex.Replace(m, "(?i)required", "必填");
            m = Regex.Replace(m, "(?i)invalid", "无效");
            m = Regex.Replace(m, "(?i)not valid", "无效");
            return m;
        }
    }
}
