using OnlineDama.Models;

namespace OnlineDama.Services
{
    public class GameService
    {
        public bool IsInside(int row, int col)
        {
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }

        public bool IsRedPiece(string piece)
        {
            return piece == "r" || piece == "R";
        }

        public bool IsBlackPiece(string piece)
        {
            return piece == "b" || piece == "B";
        }

        public bool IsKing(string piece)
        {
            return piece == "R" || piece == "B";
        }

        public bool BelongsToPlayer(string piece, string player)
        {
            if (player == "r") return IsRedPiece(piece);
            if (player == "b") return IsBlackPiece(piece);
            return false;
        }

        public bool IsOpponentPiece(string piece, string player)
        {
            if (piece == "") return false;

            if (player == "r") return IsBlackPiece(piece);
            if (player == "b") return IsRedPiece(piece);

            return false;
        }

        public List<(int dr, int dc)> GetDirections(string piece)
        {
            var directions = new List<(int dr, int dc)>();

            if (piece == "r")
            {
                directions.Add((-1, -1));
                directions.Add((-1, 1));
            }
            else if (piece == "b")
            {
                directions.Add((1, -1));
                directions.Add((1, 1));
            }
            else if (piece == "R" || piece == "B")
            {
                directions.Add((-1, -1));
                directions.Add((-1, 1));
                directions.Add((1, -1));
                directions.Add((1, 1));
            }

            return directions;
        }

        public void PromoteIfNeeded(GameState game, int row, int col)
        {
            var piece = game.Board[row][col];

            if (piece == "r" && row == 0)
            {
                game.Board[row][col] = "R";
            }
            else if (piece == "b" && row == 7)
            {
                game.Board[row][col] = "B";
            }
        }

        public List<(int toRow, int toCol)> GetSimpleMoves(GameState game, int row, int col)
        {
            var result = new List<(int toRow, int toCol)>();
            var piece = game.Board[row][col];

            foreach (var (dr, dc) in GetDirections(piece))
            {
                int newRow = row + dr;
                int newCol = col + dc;

                if (IsInside(newRow, newCol) && game.Board[newRow][newCol] == "")
                {
                    result.Add((newRow, newCol));
                }
            }

            return result;
        }

        public List<(int toRow, int toCol, int capturedRow, int capturedCol)> GetCaptureMoves(GameState game, int row, int col, string player)
        {
            var result = new List<(int toRow, int toCol, int capturedRow, int capturedCol)>();
            var piece = game.Board[row][col];

            foreach (var (dr, dc) in GetDirections(piece))
            {
                int midRow = row + dr;
                int midCol = col + dc;
                int landRow = row + 2 * dr;
                int landCol = col + 2 * dc;

                if (!IsInside(midRow, midCol) || !IsInside(landRow, landCol))
                    continue;

                if (IsOpponentPiece(game.Board[midRow][midCol], player) &&
                    game.Board[landRow][landCol] == "")
                {
                    result.Add((landRow, landCol, midRow, midCol));
                }
            }

            return result;
        }

        public bool PlayerHasCapture(GameState game, string player)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var piece = game.Board[row][col];

                    if (BelongsToPlayer(piece, player))
                    {
                        var captures = GetCaptureMoves(game, row, col, player);
                        if (captures.Count > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        public bool IsValidMove(GameState game, Move move)
        {
            if (game.GameOver)
                return false;

            if (move.Player != game.CurrentPlayer)
                return false;

            if (!IsInside(move.FromRow, move.FromCol) || !IsInside(move.ToRow, move.ToCol))
                return false;

            var piece = game.Board[move.FromRow][move.FromCol];

            if (piece == "")
                return false;

            if (!BelongsToPlayer(piece, move.Player))
                return false;

            if (game.Board[move.ToRow][move.ToCol] != "")
                return false;

            if (game.ForcedRow.HasValue && game.ForcedCol.HasValue)
            {
                if (move.FromRow != game.ForcedRow.Value || move.FromCol != game.ForcedCol.Value)
                    return false;
            }

            bool mustCapture = PlayerHasCapture(game, move.Player);

            var captureMoves = GetCaptureMoves(game, move.FromRow, move.FromCol, move.Player);
            bool isCapture = captureMoves.Any(c => c.toRow == move.ToRow && c.toCol == move.ToCol);

            if (mustCapture)
                return isCapture;

            var simpleMoves = GetSimpleMoves(game, move.FromRow, move.FromCol);
            bool isSimple = simpleMoves.Any(m => m.toRow == move.ToRow && m.toCol == move.ToCol);

            return isSimple || isCapture;
        }

        public void ApplyMove(GameState game, Move move)
        {
            var piece = game.Board[move.FromRow][move.FromCol];
            int rowDiff = move.ToRow - move.FromRow;
            int colDiff = move.ToCol - move.FromCol;

            bool isCapture = Math.Abs(rowDiff) == 2 && Math.Abs(colDiff) == 2;

            game.Board[move.ToRow][move.ToCol] = piece;
            game.Board[move.FromRow][move.FromCol] = "";

            if (isCapture)
            {
                int capturedRow = move.FromRow + rowDiff / 2;
                int capturedCol = move.FromCol + colDiff / 2;
                game.Board[capturedRow][capturedCol] = "";
            }

            PromoteIfNeeded(game, move.ToRow, move.ToCol);

            if (isCapture)
            {
                var nextCaptures = GetCaptureMoves(game, move.ToRow, move.ToCol, move.Player);

                if (nextCaptures.Count > 0)
                {
                    game.ForcedRow = move.ToRow;
                    game.ForcedCol = move.ToCol;
                    return;
                }
            }

            game.ForcedRow = null;
            game.ForcedCol = null;
            game.CurrentPlayer = game.CurrentPlayer == "r" ? "b" : "r";
        }

        public int CountPieces(GameState game, string player)
        {
            int count = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (BelongsToPlayer(game.Board[row][col], player))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public bool PlayerHasAnyMove(GameState game, string player)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var piece = game.Board[row][col];

                    if (!BelongsToPlayer(piece, player))
                        continue;

                    var captures = GetCaptureMoves(game, row, col, player);
                    if (captures.Count > 0)
                        return true;

                    var simpleMoves = GetSimpleMoves(game, row, col);
                    if (simpleMoves.Count > 0)
                        return true;
                }
            }

            return false;
        }

        public void UpdateGameStatus(GameState game)
        {
            int redCount = CountPieces(game, "r");
            int blackCount = CountPieces(game, "b");

            if (redCount == 0)
            {
                game.GameOver = true;
                game.Winner = "b";
                return;
            }

            if (blackCount == 0)
            {
                game.GameOver = true;
                game.Winner = "r";
                return;
            }

            if (!PlayerHasAnyMove(game, game.CurrentPlayer))
            {
                game.GameOver = true;
                game.Winner = game.CurrentPlayer == "r" ? "b" : "r";
            }
        }
    }
}