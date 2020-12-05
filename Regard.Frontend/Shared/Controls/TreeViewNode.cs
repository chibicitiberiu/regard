using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Regard.Shared.Controls
{
    public class TreeViewNode<TData> : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isEnabled = true;
        public bool IsEnabled 
        {
            get => isEnabled;
            set => SetField(ref isEnabled, value);
        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get => isSelected;
            set => SetField(ref isSelected, value);
        }

        private bool isExpanded = true;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetField(ref isExpanded, value);
        }

        private TData data = default;
        public TData Data 
        { 
            get => data;
            set => SetField(ref data, value);
        }

        public ObservableCollection<TreeViewNode<TData>> Children { get; } = new ObservableCollection<TreeViewNode<TData>>();

        protected void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                NotifyPropertyChanged(propertyName);
            }
        }

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
