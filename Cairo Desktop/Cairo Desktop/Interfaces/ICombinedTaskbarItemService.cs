using CairoDesktop.ViewModels;
using System.Collections.ObjectModel;

namespace CairoDesktop.Interfaces
{
    public interface ICombinedTaskbarItemService
    {
        ObservableCollection<CombinedTaskbarItem> Pinned { get; }
        ObservableCollection<CombinedTaskbarItem> Unpinned { get; }
        void HookWinN();
        void UnhookWinN();
    }
}
