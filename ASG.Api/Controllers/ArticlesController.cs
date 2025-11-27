using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArticlesController : ControllerBase
    {
        private readonly IArticleService _articleService;

        public ArticlesController(IArticleService articleService)
        {
            _articleService = articleService;
        }

        /// <summary>
        /// 获取文章列表（分页，支持排序：views/createdAt/likes；支持 query 触发搜索）
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<ArticleDto>>> GetArticles([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortBy = null, [FromQuery] bool desc = true, [FromQuery] string? query = null)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResult = await _articleService.SearchArticlesAsync(query!, page, pageSize, sortBy, desc);
                return Ok(searchResult);
            }
            else
            {
                var result = await _articleService.GetArticlesAsync(page, pageSize, sortBy, desc);
                return Ok(result);
            }
        }

        /// <summary>
        /// 搜索文章（按标题或内容，分页并支持排序）
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<PagedResult<ArticleDto>>> SearchArticles([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortBy = null, [FromQuery] bool desc = true)
        {
            var result = await _articleService.SearchArticlesAsync(query, page, pageSize, sortBy, desc);
            return Ok(result);
        }

        /// <summary>
        /// 获取文章详情
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleDto>> GetArticleById(Guid id)
        {
            var result = await _articleService.GetArticleByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>
        /// 创建文章（需要登录）
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ArticleDto>> CreateArticle([FromBody] CreateArticleDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var created = await _articleService.CreateArticleAsync(dto, userId);
            return CreatedAtAction(nameof(GetArticleById), new { id = created.Id }, created);
        }

        /// <summary>
        /// 获取文章评论（分页）
        /// </summary>
        [HttpGet("{id}/comments")]
        public async Task<ActionResult<PagedResult<CommentDto>>> GetComments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _articleService.GetCommentsAsync(id, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// 发表评论（需要登录）
        /// </summary>
        [HttpPost("{id}/comments")]
        [Authorize]
        public async Task<ActionResult<CommentDto>> AddComment(Guid id, [FromBody] CreateCommentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var created = await _articleService.AddCommentAsync(id, dto, userId);
            return Ok(created);
        }

        /// <summary>
        /// 给文章点赞
        /// </summary>
        [HttpPost("{id}/like")]
        public async Task<ActionResult<int>> LikeArticle(Guid id)
        {
            try
            {
                var likes = await _articleService.LikeArticleAsync(id);
                return Ok(likes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "点赞失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 增加文章浏览量
        /// </summary>
        [HttpPost("{id}/view")]
        public async Task<ActionResult<int>> AddView(Guid id)
        {
            try
            {
                var views = await _articleService.AddViewAsync(id);
                return Ok(views);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "增加浏览量失败", error = ex.Message });
            }
        }
    }
}