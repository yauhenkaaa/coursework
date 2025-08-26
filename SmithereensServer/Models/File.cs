using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithereensServer.Models
{
    public class File
    {
        public int FileId { get; set; }              // Уникальный идентификатор файла (PK, автоинкремент)
        public string FileName { get; set; }         // Имя файла (NOT NULL)
        public string FileType { get; set; }         // Тип файла (опционально)
        public int? FileSize { get; set; }
        public DateTime UploadTimestamp { get; set; } // Время загрузки (NOT NULL)
    }
}
