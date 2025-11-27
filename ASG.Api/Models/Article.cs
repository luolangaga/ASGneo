using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class Article
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string ContentMarkdown { get; set; } = string.Empty;

        [Required]
        public string AuthorUserId { get; set; } = string.Empty;

        [ForeignKey("AuthorUserId")]
        public virtual User? Author { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 新增：点赞数与浏览量
        public int Likes { get; set; } = 0;
        public int Views { get; set; } = 0;

        public virtual List<Comment> Comments { get; set; } = new List<Comment>();
    }
}