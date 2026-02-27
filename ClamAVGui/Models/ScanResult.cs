using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ClamAVGui.Models
{
    public class ScanResult : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(nameof(FilePath)); } }

        private string _status = string.Empty;
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }

        public bool IsFolder { get; set; }

        public ObservableCollection<ScanResult> Children { get; set; }

        public ScanResult()
        {
            Children = new ObservableCollection<ScanResult>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
