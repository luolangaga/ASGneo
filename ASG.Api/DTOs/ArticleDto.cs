using System.ComponentModel.DataAnnotations;

namespace ASG.Api.DTOs
{
    public class ArticleDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ContentMarkdown { get; set; } = string.Empty;
        public string AuthorUserId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public Guid? AuthorTeamId { get; set; }
        public string? AuthorTeamName { get; set; }
        public List<EventDto>? Honors { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Likes { get; set; }
        public int Views { get; set; }
        public int CommentsCount { get; set; }
    }

    public class CreateArticleDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string ContentMarkdown { get; set; } = string.Empty;
    }

    public class CommentDto
    {
        public Guid Id { get; set; }
        public Guid ArticleId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AuthorUserId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
        public string? AuthorAvatarUrl { get; set; }
        public Guid? ParentId { get; set; }
        public List<CommentDto>? Replies { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateCommentDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }
}