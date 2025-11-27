using System.Collections.Generic;

namespace ASG.Api.DTOs
{
    /// <summary>
    /// 综合搜索返回结果（可选包含：战队、赛事、文章）
    /// </summary>
    public class SearchAggregateDto
    {
        public PagedResult<TeamDto>? Teams { get; set; }
        public PagedResult<EventDto>? Events { get; set; }
        public PagedResult<ArticleDto>? Articles { get; set; }
    }
}