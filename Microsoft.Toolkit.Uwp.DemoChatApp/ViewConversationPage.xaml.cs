using Microsoft.Toolkit.Uwp.DemoChatApp.ViewLayer;
using Microsoft.Toolkit.Uwp.DemoChatApp.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Microsoft.Toolkit.Uwp.DemoChatApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ViewConversationPage : Page
    {
        public ViewConversationPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += ViewConversationPage_BackRequested;

            Conversation = (ViewItemConversation)e.Parameter;
            MainViewModel.Current.MarkOpenedConversation(Conversation);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            SystemNavigationManager.GetForCurrentView().BackRequested -= ViewConversationPage_BackRequested;
        }

        private void ViewConversationPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            base.Frame.Navigate(typeof(MainPage));
            e.Handled = true;
        }

        public ViewItemConversation Conversation { get; private set; }
    }
}
