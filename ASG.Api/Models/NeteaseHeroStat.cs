using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class NeteaseHeroStat
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public int HeroId { get; set; }

        [Required]
        public int CampId { get; set; }

        [Required]
        public int Season { get; set; }

        [Required]
        public int Part { get; set; }

        public int? WeekNum { get; set; }

        [Required]
        public double WinRate { get; set; }

        [Required]
        public double PingRate { get; set; }

        [Required]
        public double UseRate { get; set; }

        [Required]
        public double BanRate { get; set; }

        public double? RunRate { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(200)]
        public string Position { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = "netease";

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        public NeteaseHero? Hero { get; set; }
    }
}
