using System;
using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class NeteaseHero
    {
        [Key]
        public int HeroId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int CampId { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}