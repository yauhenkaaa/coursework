using System.IO;

namespace SmithereensServer.Models
{
    public class FileModel
    {
        public bool IsFileAttached { get; set; }
        public string? FileName { get; set; }
        public int? FileSize { get; set; }
        public string? FileType { get; set; }
        public string? FileBytes { get; set; }
    }
}