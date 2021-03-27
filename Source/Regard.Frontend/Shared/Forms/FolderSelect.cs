using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Common.Utils.Collections;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public class FolderSelectViewModel
    {
        public int? Id { get; set; }

        public string QualifiedName { get; set; }

        public string Name { get; set; }
    }

    internal class FolderSelectViewModelComparer : IComparer<FolderSelectViewModel>
    {
        public int Compare(FolderSelectViewModel x, FolderSelectViewModel y)
        {
            return string.Compare(x.QualifiedName, y.QualifiedName);
        }
    }

    public class FolderSelect : RgInputSelect<FolderSelectViewModel, int?>, IDisposable
    {
        protected readonly SortedSet<FolderSelectViewModel> folders = new SortedSet<FolderSelectViewModel>(new FolderSelectViewModelComparer());

        [Inject] protected AppState AppState { get; set; }

        protected override async Task OnInitializedAsync()
        {
            KeyFunc = folder => folder.Id;
            DisplayTextFunc = folder => folder.QualifiedName;
            ShowDefaultOption = false;
            ItemsSource = folders;

            await base.OnInitializedAsync();

            AppState.Folders.DictionaryChanged += Folders_DictionaryChanged;

            RepopulateFolders();
        }

        protected override void Dispose(bool disposing)
        {
            AppState.Folders.DictionaryChanged -= Folders_DictionaryChanged;
            base.Dispose(disposing);
        }

        private void RepopulateFolders()
        {
            folders.Clear();
            folders.Add(new FolderSelectViewModel() { Id = null, Name = "<none>", QualifiedName = "<none>" });
            InsertFolders(AppState.Folders.Values);
        }

        private void InsertFolders(IEnumerable<ApiSubscriptionFolder> newFolders)
        {
            Dictionary<int, string> folderNames = new Dictionary<int, string>(folders
                .Where(x => x.Id.HasValue)
                .Select(x => KeyValuePair.Create(x.Id.Value, x.QualifiedName)));

            var queue = new Queue<ApiSubscriptionFolder>(newFolders);

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();

                // Root level - just add it to the dict
                if (!item.ParentId)
                {
                    folderNames.Add(item.Id, item.Name);
                    folders.Add(new FolderSelectViewModel() { Id = item.Id, Name = item.Name, QualifiedName = item.Name });
                }
                // The parent was already added to the dict, use it to build the display name
                else if (folderNames.TryGetValue(item.ParentId.Value.Value, out string parentName))
                {
                    string qualifiedName = $"{parentName}\\{item.Name}";
                    folderNames.Add(item.Id, qualifiedName);
                    folders.Add(new FolderSelectViewModel() { Id = item.Id, Name = item.Name, QualifiedName = qualifiedName });
                }
                // Parent wasn't added to the dict yet, put back in the queue
                else
                {
                    // Push to end of queue
                    queue.Enqueue(item);
                }
            }
        }

        private void Folders_DictionaryChanged(object sender, DictionaryChangedEventArgs<int, ApiSubscriptionFolder> e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                folders.Clear();

            foreach (var oldItem in e.OldItems)
            {
                var item = folders.FirstOrDefault(x => x.Id == oldItem.Key);
                if (item != null)
                    folders.Remove(item);
            }

            InsertFolders(e.NewItems.Select(x => x.Value));
            StateHasChanged();
        }
    }
}
