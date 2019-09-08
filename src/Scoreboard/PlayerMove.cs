using System.ComponentModel.DataAnnotations;

namespace Scoreboard
{
    public class PlayerMove
    {
        [Required]
        public UserInfo Player { get; set; }

        public Shape Move { get; set; }
    }
}