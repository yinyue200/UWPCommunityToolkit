using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.DemoChatApp.Helpers;
using Microsoft.Toolkit.Uwp.DemoChatApp.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.Toolkit.Uwp.DemoChatApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private const string BACKGROUND_TASK_TOAST_HISTORY_CHANGE = "ToastHistoryChange";

        public static MainViewModel ViewModel;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await OnLaunchOrActivated(e);
        }

        private async void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            switch (e.WindowActivationState)
            {
                case Windows.UI.Core.CoreWindowActivationState.CodeActivated:
                case Windows.UI.Core.CoreWindowActivationState.PointerActivated:
                    WindowHelper.IsActive = true;
                    await MainViewModel.Current.OnWindowActivated();
                    break;

                default:
                    WindowHelper.IsActive = false;
                    break;
            }
        }

        private bool _initialized;
        private async Task OnLaunchOrActivated(IActivatedEventArgs args)
        {
            if (!_initialized)
            {
                _initialized = true;

                await BackgroundExecutionManager.RequestAccessAsync();

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals(BACKGROUND_TASK_TOAST_HISTORY_CHANGE)))
                {
                    BackgroundTaskBuilder builder = new BackgroundTaskBuilder()
                    {
                        Name = BACKGROUND_TASK_TOAST_HISTORY_CHANGE
                    };
                    builder.SetTrigger(new ToastNotificationHistoryChangedTrigger());
                    builder.Register();
                }

#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    this.DebugSettings.EnableFrameRateCounter = true;
                }
#endif

                MainViewModel.Initialize(Window.Current.Dispatcher);

                Frame rootFrame = Window.Current.Content as Frame;

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (rootFrame == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    rootFrame = new Frame();

                    rootFrame.NavigationFailed += OnNavigationFailed;
                    rootFrame.Navigated += RootFrame_Navigated;

                    // Place the frame in the current Window
                    Window.Current.Content = rootFrame;
                }

                Window.Current.Activated += Current_Activated;
            }

            HandleActivation(args);

            // Ensure the current window is active
            Window.Current.Activate();
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            (sender as Frame).BackStack.Clear();
        }

        private void HandleActivation(IActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            var toastActivationArgs = args as ToastNotificationActivatedEventArgs;
            if (toastActivationArgs != null)
            {
                int convId = int.Parse(toastActivationArgs.Argument);
                var conv = MainViewModel.Current.Conversations.FirstOrDefault(i => i.Id == convId);
                if (conv != null)
                {
                    if (MainViewModel.Current.CurrentOpenConversation != conv)
                    {
                        rootFrame.Navigate(typeof(ViewConversationPage), conv);
                        return;
                    }
                }
            }

            var launchArgs = args as LaunchActivatedEventArgs;
            if (launchArgs.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage));
                }
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            OnLaunchOrActivated(args);
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            var deferral = args.TaskInstance.GetDeferral();

            switch (args.TaskInstance.Task.Name)
            {
                case BACKGROUND_TASK_TOAST_HISTORY_CHANGE:
                    await HandleToastChangeAsync();
                    break;
            }

            deferral.Complete();
        }

        private async Task HandleToastChangeAsync()
        {
            var viewModel = MainViewModel.Current;
            if (viewModel == null)
            {
                // In a real app, we would actually load the view model
                // but in this app, we only execute while the app is open
                return;
            }

            try
            {
                var reader = await ToastHistoryChangeTracker.Current.GetChangeReaderAsync();
                var changes = await reader.ReadChangesAsync();
                bool madeChanges = false;

                foreach (var c in changes)
                {
                    if (c.ChangeType == ToastHistoryChangeType.Removed && c.Group.Equals("conversations"))
                    {
                        int convId = int.Parse(c.Tag);

                        var conv = viewModel.Conversations.FirstOrDefault(i => i.Id == convId);
                        if (conv != null)
                        {
                            conv.MarkNotificationDismissed();
                            madeChanges = true;
                        }
                    }
                }

                // In a real app, we would also want to save changes to our view model
                await reader.AcceptChangesAsync();

                if (madeChanges)
                {
                    TileHelper.Update();
                }

                await ToastHistoryChangeTracker.Current.SavingTask;
            }
            catch { }
        }
    }
}
