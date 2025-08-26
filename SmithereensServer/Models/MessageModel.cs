using System;
using System.IO;

namespace SmithereensServer.Models
{
    public class MessageModel
    {
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public string Username { get; set; }
        public string UsernameColor { get; set; }
        public string ImageSource { get; set; }
        public string Message { get; set; }
        public DateTime MessageTime { get; set; }
        public string ReadableTime => MessageTime.ToShortTimeString();
        public bool IsNativeOrigin { get; set; }
        public bool? FirstMessage { get; set; }
        public int MessageID { get; set; } 
        public DateTime Timestamp { get; set; }      // Время отправки (NOT NULL)
        public bool IsEdited { get; set; }           // Флаг редактирования (по умолчанию FALSE)
        public bool IsDeleted { get; set; }
        public int? FileId { get; set; } // для БД
        public bool IsFileAttached { get; set; }
        public string? FileName { get; set; }
        public int? FileSize { get; set; }
    }
}