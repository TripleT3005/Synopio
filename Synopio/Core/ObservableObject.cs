using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Synopio.Core;

public abstract class ObservableObject : INotifyPropertyChanged
{
    // Events
    public event PropertyChangedEventHandler? PropertyChanged;

    // Methods
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
