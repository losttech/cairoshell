using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;

using CairoDesktop.AppGrabber;
using CairoDesktop.Common;
using CairoDesktop.Configuration;
using CairoDesktop.Infrastructure.Services;
using CairoDesktop.Interfaces;
using CairoDesktop.SupportingClasses;
using CairoDesktop.ViewModels;

using Gma.System.MouseKeyHook;

using ManagedShell.WindowsTasks;

using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace CairoDesktop.Services
{
    internal class CombinedTaskbarItemService : ICombinedTaskbarItemService, IDisposable
    {
        readonly IAppGrabber _appGrabber;
        readonly IKeyboardEvents _keyHook;
        readonly Category _pinned;
        readonly ICollectionView _running;

        public ObservableCollection<CombinedTaskbarItem> Pinned { get; }
            = new ObservableCollection<CombinedTaskbarItem>();
        public ObservableCollection<CombinedTaskbarItem> Unpinned { get; }
            = new ObservableCollection<CombinedTaskbarItem>();

        public CombinedTaskbarItemService(IAppGrabber appGrabber, IKeyboardEvents keyHook, ShellManagerService shellManagerService)
        {
            _appGrabber = appGrabber;
            _keyHook = keyHook;

            _pinned = appGrabber.QuickLaunch;
            _pinned.CollectionChanged += Pinned_CollectionChanged;

            foreach (ApplicationInfo app in _pinned)
            {
                Pinned.Add(new CombinedTaskbarItem
                {
                    AppInfo = app,
                });
            }

            var tasks = (ObservableCollection<ApplicationWindow>)shellManagerService.ShellManager.Tasks.GroupedWindows.SourceCollection;
            _running = new CollectionViewSource { Source = tasks }.View;
            _running.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ApplicationWindow.Category)));
            _running.Filter += Running_Filter;
            _running.CollectionChanged += Running_CollectionChanged;

            if (_running is ICollectionViewLiveShaping tasksView)
            {
                tasksView.IsLiveFiltering = true;
                tasksView.LiveFilteringProperties.Add(nameof(ApplicationWindow.ShowInTaskbar));
                tasksView.IsLiveGrouping = true;
                tasksView.LiveGroupingProperties.Add(nameof(ApplicationWindow.Category));
            }

            ((INotifyCollectionChanged)_running.Groups).CollectionChanged += RunningGroups_CollectionChanged;
        }

        bool winN = false;
        public void HookWinN()
        {
            if (winN) return;
            winN = true;
            _keyHook.KeyDown += KeyHook_KeyDown;
        }

        void KeyHook_KeyDown(object sender, KeyEventArgs e)
        {
            WinN(e);
        }

        public void UnhookWinN()
        {
            if (!winN) return;
            winN = false;
            _keyHook.KeyDown -= KeyHook_KeyDown;
        }

        async void WinN(KeyEventArgs e)
        {
            if (HotKey.GetKeyboardModifiers() != ModifierKeys.Windows) return;

            Key key = KeyInterop.KeyFromVirtualKey((int)e.KeyData);

            int n = key - Key.D0;
            int index = n == 0 ? 9 : n - 1;
            CombinedTaskbarItem item = null;
            if (index < Pinned.Count)
            {
                item = Pinned[index];
            }
            else if (index < Pinned.Count + Unpinned.Count)
            {
                item = Unpinned[index - Pinned.Count];
            }

            e.Handled = item != null;

            await Task.Yield();

            item?.Activate(_appGrabber);
        }

        void RunningGroups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CollectionViewGroup group in e.NewItems)
                {
                    var path = (string)group.Name;
                    var pinned = Pinned.FirstOrDefault(i => PathEquals(path, i.AppInfo.Target));
                    if (pinned != null)
                    {
                        pinned.TaskGroup = new NamedTaskGroup(path, group.Items);
                    }
                    else
                    {
                        Unpinned.Add(new CombinedTaskbarItem
                        {
                            TaskGroup = new NamedTaskGroup(path, group.Items),
                        });
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (CollectionViewGroup group in e.OldItems)
                {
                    var pinned = Pinned.FirstOrDefault(i => i.TaskGroup?.Windows == group.Items);
                    if (pinned != null)
                    {
                        pinned.TaskGroup = null;
                    }
                    else
                    {
                        var unpinned = Unpinned.FirstOrDefault(i => i.TaskGroup.Windows == group.Items);
                        Unpinned.Remove(unpinned);
                    }
                }
            }
        }

        void Running_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

        }

        void Pinned_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                int insertAt = e.NewStartingIndex;
                foreach (ApplicationInfo app in e.NewItems)
                {
                    string path = app.Target;
                    CombinedTaskbarItem unpinned = Unpinned.FirstOrDefault(i => PathEquals(path, ((NamedTaskGroup)i.TaskGroup).Name));
                    Pinned.Insert(insertAt, new CombinedTaskbarItem
                    {
                        AppInfo = app,
                        TaskGroup = unpinned?.TaskGroup,
                    });
                    insertAt++;
                }
            }

            if (e.OldItems != null)
            {
                foreach (ApplicationInfo app in e.OldItems)
                {
                    CombinedTaskbarItem item = Pinned.FirstOrDefault(i => i.AppInfo == app);
                    Pinned.Remove(item);
                    item.AppInfo = null;
                    if (item.TaskGroup != null)
                    {
                        Unpinned.Add(item);
                    }
                }
            }
        }

        bool Running_Filter(object obj)
        {
            if (obj is ApplicationWindow window)
            {
                return window.ShowInTaskbar;
            }

            return true;
        }

        public void Dispose()
        {
            UnhookWinN();
        }

        static bool PathEquals(string a, string b)
            => string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);

        class NamedTaskGroup : TaskGroup
        {
            public string Name { get; }
            public NamedTaskGroup(string name, ReadOnlyObservableCollection<object> windows) : base(windows)
            {
                Name = name;
            }
        }
    }
}
