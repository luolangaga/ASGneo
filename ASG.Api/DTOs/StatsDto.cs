using System;
using System.Collections.Generic;

namespace ASG.Api.DTOs
{
    public class HeroBriefDto
    {
        public int HeroId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CampId { get; set; }
    }

    public class StatPointDto
    {
        public DateTime Date { get; set; }
        public double WinRate { get; set; }
        public double PingRate { get; set; }
        public double UseRate { get; set; }
        public double BanRate { get; set; }
        public double? RunRate { get; set; }
    }

    public class OverviewItemDto
    {
        public int HeroId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CampId { get; set; }
        public double WinRateAvg { get; set; }
        public double PingRateAvg { get; set; }
        public double UseRateAvg { get; set; }
        public double BanRateAvg { get; set; }
        public double? RunRateAvg { get; set; }
        public double MetricLatest { get; set; }
    }

    public class StatsMetaDto
    {
        public List<int> Seasons { get; set; } = new();
        public List<int> Parts { get; set; } = new();
        public List<int> Camps { get; set; } = new();
        public List<string> Positions { get; set; } = new();
    }

    public class CompareSeriesDto
    {
        public int HeroId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ComparePointDto> Points { get; set; } = new();
    }

    public class ComparePointDto
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
    }
}

