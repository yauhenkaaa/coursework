using System.Collections.ObjectModel;
using System.Linq;

namespace SmithereensServer.Models
{
    public class ConvModel
    {
        public int ConvID { get; set; }
        public string ConvName { get; set; }
        public string ImageSource { get; set; }
        public ObservableCollection<MessageModel> Messages { get; set; }
        public string LastMessage => Messages.LastOrDefault()?.Message ?? "";
        public string LastMessageTime => Messages.LastOrDefault()?.MessageTime.ToShortTimeString() ?? "";
        public string LastMessageTimeFull => Messages.LastOrDefault()?.MessageTime.ToLongTimeString() ?? "";
    }
}