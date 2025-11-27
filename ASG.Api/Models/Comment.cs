using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class Comment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ArticleId { get; set; }

        [ForeignKey("ArticleId")]
        public virtual Article? Article { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public string AuthorUserId { get; set; } = string.Empty;

        [ForeignKey("AuthorUserId")]
        public virtual User? Author { get; set; }

        public Guid? ParentId { get; set; }
        public virtual Comment? Parent { get; set; }
        public virtual List<Comment> Replies { get; set; } = new List<Comment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}