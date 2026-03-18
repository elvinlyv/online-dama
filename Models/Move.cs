namespace OnlineDama.Models
{
    public class Move
    {
        public int FromRow { get; set; }
        public int FromCol { get; set; }
        public int ToRow { get; set; }
        public int ToCol { get; set; }
        public string Player { get; set; } = "";
    }
}