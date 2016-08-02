using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.Notifications
{
    /// <summary>
    /// Attribute for specifying default behaviors to the <see cref="ToastHistoryChangeTracker"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ToastHistoryChangeTrackerAttribute : Attribute
    {
        /// <summary>
        /// Initializes various settings for the <see cref="ToastHistoryChangeTracker"/>.
        /// </summary>
        /// <param name="includePayloadArguments">Specifies whether the tracker should pull out and store the
        /// original Toast launch argument from the Toast XML payload.</param>
        /// <param name="includePayload">Specifies whether the tracker should store the original
        /// Toast XML payload.</param>
        public ToastHistoryChangeTrackerAttribute(bool includePayloadArguments = false, bool includePayload = false)
        {
            IncludePayloadArguments = includePayloadArguments;
            IncludePayload = includePayload;
        }

        /// <summary>
        /// Gets a boolean representing whether the tracker should store the original Toast XML payload.
        /// </summary>
        public bool IncludePayload { get; private set; }

        /// <summary>
        /// Gets a boolean representing whether the tracker should pull out and store
        /// the original Toast launch argument from the payload.
        /// </summary>
        public bool IncludePayloadArguments { get; private set; }
    }

    internal static class ToastHistoryChangeTrackerConfiguration
    {
        private static ToastHistoryChangeTrackerAttribute _current;

        public static ToastHistoryChangeTrackerAttribute Current
        {
            get { return _current; }
        }

        public static async Task InitializeAsync()
        {
            if (_current == null)
            {
                try
                {
                    // In order to obtain the parent app's AssemblyInfo, need to get the parent's assembly
                    // AppDomain doesn't exist in WinRT, so we do the following
                    // https://gist.github.com/grumpydev/1234767
                    var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

                    var queryResult = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions()
                    {
                        ApplicationSearchFilter = "ext:.exe",
                        FolderDepth = Windows.Storage.Search.FolderDepth.Shallow
                    });
                    var file = (await queryResult.GetFilesAsync(0, 1)).FirstOrDefault();
                    if (file == null)
                    {
                        _current = new Notifications.ToastHistoryChangeTrackerAttribute();
                    }
                    else
                    {
                        Assembly assembly = Assembly.Load(new AssemblyName() { Name = System.IO.Path.GetFileNameWithoutExtension(file.Name) });

                        // Then we need to get the custom attribute from AssemblyInfo.cs
                        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/14ee99ed-379b-4646-8e3e-bb747312e608/adding-new-values-to-assemblyinfocs
                        var attr = assembly.GetCustomAttribute<ToastHistoryChangeTrackerAttribute>();
                        _current = attr;
                    }
                }
                catch
                {
                    _current = new Notifications.ToastHistoryChangeTrackerAttribute();
                }
            }
        }
    }
}
