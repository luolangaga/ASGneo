using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IArticleService
    {
        Task<PagedResult<ArticleDto>> GetArticlesAsync(int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true);
        Task<PagedResult<ArticleDto>> SearchArticlesAsync(string query, int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true);
        Task<ArticleDto?> GetArticleByIdAsync(Guid id);
        Task<ArticleDto> CreateArticleAsync(CreateArticleDto dto, string authorUserId);

        Task<PagedResult<CommentDto>> GetCommentsAsync(Guid articleId, int page = 1, int pageSize = 20);
        Task<CommentDto> AddCommentAsync(Guid articleId, CreateCommentDto dto, string authorUserId);

        Task<int> LikeArticleAsync(Guid id);
        Task<int> AddViewAsync(Guid id);
    }
}