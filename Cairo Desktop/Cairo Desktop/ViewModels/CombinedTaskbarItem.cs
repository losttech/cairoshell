using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

using CairoDesktop.AppGrabber;
using CairoDesktop.SupportingClasses;

using ManagedShell.Interop;
using ManagedShell.WindowsTasks;

namespace CairoDesktop.ViewModels
{
    public class CombinedTaskbarItem : INotifyPropertyChanged
    {
        TaskGroup _taskGroup;
        public TaskGroup TaskGroup
        {
            get => _taskGroup;
            set
            {
                bool changing = _taskGroup != value;
                if (changing)
                {
                    if (_taskGroup != null) _taskGroup.PropertyChanged -= TaskGroup_PropertyChanged;
                    if (value != null) value.PropertyChanged += TaskGroup_PropertyChanged;
                }
                _taskGroup = value;
                OnPropertyChanged();
                if (changing)
                {
                    foreach(var computedProperty in ComputedProperties)
                    {
                        OnPropertyChanged(computedProperty);
                    }
                }
            }
        }

        ApplicationInfo _appInfo;
        public ApplicationInfo AppInfo
        {
            get => _appInfo;
            set
            {
                if (_appInfo != value)
                {
                    if (_appInfo != null) _appInfo.PropertyChanged -= AppInfo_PropertyChanged;
                    if (value != null) value.PropertyChanged += AppInfo_PropertyChanged;
                }
                _appInfo = value;
                OnPropertyChanged();
            }
        }

        void AppInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
            switch (e.PropertyName)
            {
                case nameof(AppInfo.Name):
                    OnPropertyChanged(nameof(TaskGroup.Title));
                    break;
            }
        }

        void TaskGroup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        public ImageSource Icon => TaskGroup?.Icon ?? AppInfo?.Icon;
        public string Title => TaskGroup?.Title ?? AppInfo?.Name;
        public bool ShowInTaskbar => true;
        public ImageSource OverlayIcon => TaskGroup?.OverlayIcon;
        public string OverlayIconDescription => TaskGroup?.OverlayIconDescription;
        public NativeMethods.TBPFLAG ProgressState => TaskGroup?.ProgressState ?? NativeMethods.TBPFLAG.TBPF_NOPROGRESS;
        public int ProgressValue => TaskGroup?.ProgressValue ?? 0;
        public ApplicationWindow.WindowState State => TaskGroup?.State ?? ApplicationWindow.WindowState.Hidden;

        public void Activate(IAppGrabber appGrabber)
        {
            // TODO cycle through open
            if (TaskGroup?.Windows.Count > 0)
            {
                var window = (ApplicationWindow)TaskGroup.Windows[0];
                if (window.State == ApplicationWindow.WindowState.Active)
                {
                    window.Minimize();
                }
                else
                {
                    window.BringToFront();
                }
            }
            else if (AppInfo != null)
            {
                appGrabber.LaunchProgram(AppInfo);
            }
        }

        static readonly string[] ComputedProperties =
        {
            nameof(Icon), nameof(Title), nameof(ShowInTaskbar),
            nameof(OverlayIcon), nameof(OverlayIconDescription),
            nameof(ProgressState), nameof(ProgressValue),
            nameof(State),
        };

        public override string ToString() => this.Title;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
