using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithereensServer.Models
{
    public class ConversationParticipants
    {
        public int ConversationID { get; set; }      // Идентификатор беседы (часть PK)
        public int UserID { get; set; }              // Идентификатор пользователя (часть PK)

        // Навигационные свойства
        public Conversation Conversation { get; set; } // Связанная беседа
        public User User { get; set; }                 // Связанный пользователь
    }
}
