using System.ComponentModel.DataAnnotations;

namespace BotPlayer
{
    public class UserInfo
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        public bool IsBot { get; set; }
    }
}