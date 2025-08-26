using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithereensServer.Models
{
    public class Conversation
    {
        public int ConversationID { get; set; }      // Уникальный идентификатор беседы (PK, автоинкремент)
        public string ConversationName { get; set; } // Название беседы (опционально)
        public bool IsGroupChat { get; set; }        // Флаг групповой беседы (NOT NULL, по умолчанию FALSE)

        // Навигационные свойства
        public ICollection<Message> Messages { get; set; }                    // Сообщения в беседе
        public ICollection<ConversationParticipants> ConversationParticipants { get; set; } // Участники беседы
    }
}
