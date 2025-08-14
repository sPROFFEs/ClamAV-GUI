using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClamAVGui.Models
{
    public class ScanSummary : INotifyPropertyChanged
    {
        private string _knownViruses = "0";
        public string KnownViruses
        {
            get => _knownViruses;
            set { _knownViruses = value; OnPropertyChanged(); }
        }

        private string _engineVersion = "";
        public string EngineVersion
        {
            get => _engineVersion;
            set { _engineVersion = value; OnPropertyChanged(); }
        }

        private string _scannedDirectories = "0";
        public string ScannedDirectories
        {
            get => _scannedDirectories;
            set { _scannedDirectories = value; OnPropertyChanged(); }
        }

        private string _scannedFiles = "0";
        public string ScannedFiles
        {
            get => _scannedFiles;
            set { _scannedFiles = value; OnPropertyChanged(); }
        }

        private string _infectedFiles = "0";
        public string InfectedFiles
        {
            get => _infectedFiles;
            set { _infectedFiles = value; OnPropertyChanged(); }
        }

        private string _timeTaken = "";
        public string TimeTaken
        {
            get => _timeTaken;
            set { _timeTaken = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
