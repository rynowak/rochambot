using Rochambot;

namespace GameMaster
{
    public static class ScoringExtensions
    {
        public static Round DetermineScore(this Round result) =>
            (result.PlayerShape, result.OpponentShape) switch
        {
            (Shape.Paper, Shape.Rock) => result.PaperCoversRock(),
            (Shape.Paper, Shape.Scissors) => result.PaperCutByScissors(),
            (Shape.Rock, Shape.Paper) => result.RockCoveredByPaper(),
            (Shape.Rock, Shape.Scissors) => result.RockBreaksScissors(),
            (Shape.Scissors, Shape.Paper) => result.ScissorsCutPaper(),
            (Shape.Scissors, Shape.Rock) => result.ScissorsBrokenByRock(),
            (_, _) => result.TieGame(),
        };

        static Round PaperCoversRock(this Round round)
        {
            round.Summary = "You win! Paper covers rock.";
            round.PlayerWins = true;
            return round;
        }

        static Round PaperCutByScissors(this Round round)
        {
            round.Summary = "You lose! Scissors cut paper.";
            round.PlayerWins = false;
            return round;
        }
        static Round RockCoveredByPaper(this Round round)
        {
            round.Summary = "You lose! Paper covers rock.";
            round.PlayerWins = false;
            return round;
        }
        static Round RockBreaksScissors(this Round round)
        {
            round.Summary = "You win! Rock breaks scissors.";
            round.PlayerWins = true;
            return round;
        }

        static Round ScissorsCutPaper(this Round round)
        {
            round.Summary = "You win! Scissors cut paper.";
            round.PlayerWins = true;
            return round;
        }

        static Round ScissorsBrokenByRock(this Round round)
        {
            round.Summary = "You lose. Rock breaks scissors.";
            round.PlayerWins = false;
            return round;
        }

        static Round TieGame(this Round round)
        {
            round.Summary = "Tie";
            round.Tie = true;
            round.PlayerWins = false;
            return round;
        }
    }
}