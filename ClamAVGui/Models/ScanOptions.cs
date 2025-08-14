using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClamAVGui.Models
{
    public class ScanOptions : INotifyPropertyChanged
    {
        private bool _moveToQuarantine;
        public bool MoveToQuarantine
        {
            get => _moveToQuarantine;
            set { _moveToQuarantine = value; OnPropertyChanged(); }
        }

        private string _quarantinePath = "";
        public string QuarantinePath
        {
            get => _quarantinePath;
            set { _quarantinePath = value; OnPropertyChanged(); }
        }

        private bool _heuristicAlerts;
        public bool HeuristicAlerts
        {
            get => _heuristicAlerts;
            set { _heuristicAlerts = value; OnPropertyChanged(); }
        }

        private bool _scanEncrypted;
        public bool ScanEncrypted
        {
            get => _scanEncrypted;
            set { _scanEncrypted = value; OnPropertyChanged(); }
        }

        private bool _leaveTemps;
        public bool LeaveTemps
        {
            get => _leaveTemps;
            set { _leaveTemps = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
