using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithereensServer.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public string? ProfilePicture { get; set; }

        // Навигационные свойства
        public ICollection<Message> Messages { get; set; }
        public ICollection<ConversationParticipants> ConversationParticipants { get; set; }
    }
}
