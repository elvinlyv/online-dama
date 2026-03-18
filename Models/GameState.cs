namespace OnlineDama.Models
{
    public class GameState
    {
        public string[][] Board { get; set; } = new string[8][];
        public string CurrentPlayer { get; set; } = "r";
        public bool GameOver { get; set; } = false;
        public string Winner { get; set; } = "";
        public int? ForcedRow { get; set; }
        public int? ForcedCol { get; set; }

        public GameState()
        {
            Board = new string[][]
            {
                new string[] { "", "b", "", "b", "", "b", "", "b" },
                new string[] { "b", "", "b", "", "b", "", "b", "" },
                new string[] { "", "b", "", "b", "", "b", "", "b" },
                new string[] { "", "", "", "", "", "", "", "" },
                new string[] { "", "", "", "", "", "", "", "" },
                new string[] { "r", "", "r", "", "r", "", "r", "" },
                new string[] { "", "r", "", "r", "", "r", "", "r" },
                new string[] { "r", "", "r", "", "r", "", "r", "" }
            };
        }
    }
}