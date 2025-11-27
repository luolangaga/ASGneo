namespace ASG.Api.Models
{
    public enum Camp
    {
        Regulator = 1,
        Survivor = 2
    }

    public static class CampExtensions
    {
        public static string ToChinese(this Camp c)
        {
            return c switch
            {
                Camp.Regulator => "监管者",
                Camp.Survivor => "求生者",
                _ => c.ToString()
            };
        }
    }
}

