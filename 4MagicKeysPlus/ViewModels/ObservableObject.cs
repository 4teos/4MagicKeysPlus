using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace _4MagicKeysPlus.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        Notify(propertyName!);
        return true;
    }

    protected void Notify(params string[] propertyNames) {
        foreach (string propertyName in propertyNames) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
