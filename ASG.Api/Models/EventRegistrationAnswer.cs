using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 战队报名时提交的自定义表单答案
    /// </summary>
    public class EventRegistrationAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EventId { get; set; }
        public Event? Event { get; set; }

        [Required]
        public Guid TeamId { get; set; }
        public Team? Team { get; set; }

        /// <summary>
        /// JSON 格式的回答，结构由 Event.CustomData 中的 registrationFormSchema 定义
        /// </summary>
        public string AnswersJson { get; set; } = "{}";

        public string? SubmittedByUserId { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

