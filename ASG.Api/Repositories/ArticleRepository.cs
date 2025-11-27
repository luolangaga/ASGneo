using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Repositories
{
    public class ArticleRepository : IArticleRepository
    {
        private readonly ApplicationDbContext _context;

        public ArticleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Article>> GetArticlesAsync(int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true)
        {
            IQueryable<Article> query = _context.Articles
                .Include(a => a.Author)
                    .ThenInclude(u => u.OwnedTeam)
                .Include(a => a.Comments);

            // 排序：支持 views 或 createdAt 或 likes
            switch ((sortBy ?? "createdAt").ToLowerInvariant())
            {
                case "views":
                    query = desc ? query.OrderByDescending(a => a.Views) : query.OrderBy(a => a.Views);
                    break;
                case "likes":
                    query = desc ? query.OrderByDescending(a => a.Likes) : query.OrderBy(a => a.Likes);
                    break;
                case "createdat":
                default:
                    query = desc ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt);
                    break;
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Article>> SearchArticlesAsync(string queryString, int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var q = (queryString ?? string.Empty).Trim();
            IQueryable<Article> query = _context.Articles
                .Include(a => a.Author)
                    .ThenInclude(u => u.OwnedTeam)
                .Include(a => a.Comments);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(a =>
                    EF.Functions.Like(a.Title, "%" + q + "%") ||
                    EF.Functions.Like(a.ContentMarkdown, "%" + q + "%")
                );
            }

            switch ((sortBy ?? "createdAt").ToLowerInvariant())
            {
                case "views":
                    query = desc ? query.OrderByDescending(a => a.Views) : query.OrderBy(a => a.Views);
                    break;
                case "likes":
                    query = desc ? query.OrderByDescending(a => a.Likes) : query.OrderBy(a => a.Likes);
                    break;
                case "createdat":
                default:
                    query = desc ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt);
                    break;
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchArticlesCountAsync(string queryString)
        {
            var q = (queryString ?? string.Empty).Trim();
            IQueryable<Article> query = _context.Articles;
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(a =>
                    EF.Functions.Like(a.Title, "%" + q + "%") ||
                    EF.Functions.Like(a.ContentMarkdown, "%" + q + "%")
                );
            }
            return await query.CountAsync();
        }

        public async Task<int> GetArticlesCountAsync()
        {
            return await _context.Articles.CountAsync();
        }

        public async Task<Article?> GetArticleByIdAsync(Guid id)
        {
            return await _context.Articles
                .Include(a => a.Author)
                    .ThenInclude(u => u.OwnedTeam)
                .Include(a => a.Comments)
                    .ThenInclude(c => c.Author)
                        .ThenInclude(u => u.OwnedTeam)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Article> CreateArticleAsync(Article article)
        {
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();
            return article;
        }

        public async Task<IEnumerable<Comment>> GetCommentsByArticleAsync(Guid articleId, int page = 1, int pageSize = 20)
        {
            return await _context.Comments
                .Where(c => c.ArticleId == articleId && c.ParentId == null)
                .Include(c => c.Author)
                    .ThenInclude(u => u.OwnedTeam)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Author)
                        .ThenInclude(u => u.OwnedTeam)
                .OrderBy(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCommentsCountAsync(Guid articleId)
        {
            return await _context.Comments.Where(c => c.ArticleId == articleId && c.ParentId == null).CountAsync();
        }

        public async Task<Comment> AddCommentAsync(Comment comment)
        {
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        public async Task<Comment?> GetCommentByIdAsync(Guid id)
        {
            return await _context.Comments
                .Include(c => c.Author)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<int> LikeArticleAsync(Guid id)
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) throw new InvalidOperationException("文章不存在");
            article.Likes += 1;
            article.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return article.Likes;
        }

        public async Task<int> IncrementArticleViewsAsync(Guid id)
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) throw new InvalidOperationException("文章不存在");
            article.Views += 1;
            // 浏览不一定更新 UpdatedAt；这里保持与点赞一致更新更新时间
            article.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return article.Views;
        }
    }
}