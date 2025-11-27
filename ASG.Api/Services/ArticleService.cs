using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace ASG.Api.Services
{
    public class ArticleService : IArticleService
    {
        private readonly IArticleRepository _articleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEventService _eventService;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notify;

        public ArticleService(IArticleRepository articleRepository, IUserRepository userRepository, IEventService eventService, IWebHostEnvironment env, INotificationService notify)
        {
            _articleRepository = articleRepository;
            _userRepository = userRepository;
            _eventService = eventService;
            _env = env;
            _notify = notify;
        }

        public async Task<PagedResult<ArticleDto>> GetArticlesAsync(int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true)
        {
            var articles = await _articleRepository.GetArticlesAsync(page, pageSize, sortBy, desc);
            var total = await _articleRepository.GetArticlesCountAsync();

            // 映射作者与荣誉
            var dtos = new List<ArticleDto>();
            foreach (var a in articles)
            {
                dtos.Add(await MapToArticleDtoAsync(a));
            }

            return new PagedResult<ArticleDto>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<ArticleDto>> SearchArticlesAsync(string query, int page = 1, int pageSize = 10, string? sortBy = null, bool desc = true)
        {
            var articles = await _articleRepository.SearchArticlesAsync(query, page, pageSize, sortBy, desc);
            var total = await _articleRepository.GetSearchArticlesCountAsync(query);

            var dtos = new List<ArticleDto>();
            foreach (var a in articles)
            {
                dtos.Add(await MapToArticleDtoAsync(a));
            }

            return new PagedResult<ArticleDto>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ArticleDto?> GetArticleByIdAsync(Guid id)
        {
            var article = await _articleRepository.GetArticleByIdAsync(id);
            if (article == null) return null;
            return await MapToArticleDtoAsync(article, includeComments: true);
        }

        public async Task<ArticleDto> CreateArticleAsync(CreateArticleDto dto, string authorUserId)
        {
            var user = await _userRepository.GetByIdAsync(authorUserId);
            if (user == null)
            {
                throw new InvalidOperationException("用户不存在");
            }

            var entity = new Article
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                ContentMarkdown = dto.ContentMarkdown,
                AuthorUserId = authorUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _articleRepository.CreateArticleAsync(entity);
            return await MapToArticleDtoAsync(created);
        }

        public async Task<PagedResult<CommentDto>> GetCommentsAsync(Guid articleId, int page = 1, int pageSize = 20)
        {
            var comments = await _articleRepository.GetCommentsByArticleAsync(articleId, page, pageSize);
            var total = await _articleRepository.GetCommentsCountAsync(articleId);

            return new PagedResult<CommentDto>
            {
                Items = comments.Select(MapToCommentDto),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<CommentDto> AddCommentAsync(Guid articleId, CreateCommentDto dto, string authorUserId)
        {
            var user = await _userRepository.GetByIdAsync(authorUserId);
            if (user == null)
            {
                throw new InvalidOperationException("用户不存在");
            }

            if (dto.ParentId.HasValue)
            {
                var parent = await _articleRepository.GetCommentByIdAsync(dto.ParentId.Value);
                if (parent == null || parent.ArticleId != articleId)
                {
                    throw new InvalidOperationException("父评论不存在或不属于该文章");
                }
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ArticleId = articleId,
                Content = dto.Content,
                AuthorUserId = authorUserId,
                ParentId = dto.ParentId,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _articleRepository.AddCommentAsync(comment);
            var article = await _articleRepository.GetArticleByIdAsync(articleId);
            var dtoOut = MapToCommentDto(created);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { articleId, commentId = created.Id, content = created.Content });
            if (article != null && article.AuthorUserId != authorUserId)
            {
                await _notify.NotifyUserAsync(article.AuthorUserId, "article.comment", payload);
            }
            if (created.ParentId.HasValue)
            {
                var parent = await _articleRepository.GetCommentByIdAsync(created.ParentId.Value);
                if (parent != null && parent.AuthorUserId != authorUserId)
                {
                    await _notify.NotifyUserAsync(parent.AuthorUserId, "comment.reply", payload);
                }
            }
            return dtoOut;
        }

        private async Task<ArticleDto> MapToArticleDtoAsync(Article article, bool includeComments = false)
        {
            var authorName = article.Author != null ? (article.Author.FullName ?? string.Empty) : string.Empty;
            Guid? teamId = article.Author?.TeamId;
            string? teamName = article.Author?.OwnedTeam?.Name;

            // 如果作者有队伍，查询其荣誉（冠军赛事）
            List<EventDto>? honors = null;
            if (teamId.HasValue)
            {
                var events = await _eventService.GetChampionEventsByTeamAsync(teamId.Value);
                honors = events?.ToList();
            }

            var dto = new ArticleDto
            {
                Id = article.Id,
                Title = article.Title,
                ContentMarkdown = article.ContentMarkdown,
                AuthorUserId = article.AuthorUserId,
                AuthorName = authorName,
                AuthorTeamId = teamId,
                AuthorTeamName = teamName,
                Honors = honors,
                CreatedAt = article.CreatedAt,
                UpdatedAt = article.UpdatedAt,
                Likes = article.Likes,
                Views = article.Views,
                CommentsCount = article.Comments?.Count ?? 0
            };

            if (includeComments && article.Comments != null)
            {
                // no-op here; comments由GetArticleById接口单独返回或通过另一个调用获取
            }

            return dto;
        }

        // 点赞文章
        public async Task<int> LikeArticleAsync(Guid id)
        {
            return await _articleRepository.LikeArticleAsync(id);
        }

        // 增加浏览量
        public async Task<int> AddViewAsync(Guid id)
        {
            return await _articleRepository.IncrementArticleViewsAsync(id);
        }

        private CommentDto MapToCommentDto(Comment comment)
        {
            var authorName = comment.Author != null ? (comment.Author.FullName ?? string.Empty) : string.Empty;
            var dto = new CommentDto
            {
                Id = comment.Id,
                ArticleId = comment.ArticleId,
                Content = comment.Content,
                AuthorUserId = comment.AuthorUserId,
                AuthorName = authorName,
                AuthorEmail = comment.Author?.Email ?? string.Empty,
                AuthorAvatarUrl = GetAvatarUrl(comment.AuthorUserId),
                ParentId = comment.ParentId,
                CreatedAt = comment.CreatedAt
            };
            if (comment.Replies != null && comment.Replies.Count > 0)
            {
                dto.Replies = comment.Replies
                    .OrderBy(r => r.CreatedAt)
                    .Select(MapToCommentDto)
                    .ToList();
            }
            return dto;
        }

        private string? GetAvatarUrl(string userId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var userDir = Path.Combine(root, "avatars", userId);
            if (!Directory.Exists(userDir)) return null;
            var files = Directory.GetFiles(userDir, "avatar.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            return $"/avatars/{userId}/{fileName}";
        }
    }
}