using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithereensServer.Models
{
    public class Message
    {
        public int MessageID { get; set; }           // Уникальный идентификатор сообщения (PK, автоинкремент)
        public int ConversationID { get; set; }      // Идентификатор беседы
        public int SenderID { get; set; }            // Идентификатор отправителя
        public string? Content { get; set; }          // Содержание сообщения
        public DateTime Timestamp { get; set; }      // Время отправки (NOT NULL)
        public bool IsEdited { get; set; }           // Флаг редактирования (по умолчанию FALSE)
        public bool IsDeleted { get; set; }          // Флаг удаления (по умолчанию FALSE)
        public bool? IsFileAttached { get; set; }     // Флаг прикрепления файла (по умолчанию FALSE)

        public int? FileId { get; set; }             // Идентификатор прикрепленного файла (если есть)
        
        // Навигационные свойства
        public Conversation Conversation { get; set; } // Связанная беседа
        public User Sender { get; set; }               // Отправитель сообщения
    }
}
