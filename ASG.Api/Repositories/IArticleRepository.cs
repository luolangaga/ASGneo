using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    public interface IArticleRepository
    {
        Task<IEnumerable<Article>> GetArticlesAsync(int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true);
        Task<int> GetArticlesCountAsync();

        /// <summary>
        /// 搜索文章（按标题或内容，分页并支持排序）
        /// </summary>
        Task<IEnumerable<Article>> SearchArticlesAsync(string query, int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true);

        /// <summary>
        /// 搜索文章总数（按标题或内容）
        /// </summary>
        Task<int> GetSearchArticlesCountAsync(string query);
        Task<Article?> GetArticleByIdAsync(Guid id);
        Task<Article> CreateArticleAsync(Article article);

        Task<IEnumerable<Comment>> GetCommentsByArticleAsync(Guid articleId, int page = 1, int pageSize = 20);
        Task<int> GetCommentsCountAsync(Guid articleId);
        Task<Comment> AddCommentAsync(Comment comment);
        Task<Comment?> GetCommentByIdAsync(Guid id);

        Task<int> LikeArticleAsync(Guid id);
        Task<int> IncrementArticleViewsAsync(Guid id);
    }
}