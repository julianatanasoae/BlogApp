using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace kraigb
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static string BLOG_URL = "http://kraigbrockschmidt.com/blog/";
        public static string page_title = "";
        
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();
                kraigb.Common.SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

                //TODO: replace the URI here with the actual address of the periodic update service
                List<Uri> urisToPoll = new List<Uri>(5);
                for (int i = 1; i <= 5; i++)
                {
                    //TODO: replace the URIs after uploading the files on the server
                    urisToPoll.Add(new Uri("http://www.juliandev.ro/kraigb/tile_" + i + ".php"));
                }
                TileUpdater updater = Windows.UI.Notifications.TileUpdateManager.CreateTileUpdaterForApplication();
                updater.EnableNotificationQueue(true);
                updater.StartPeriodicUpdateBatch(urisToPoll, PeriodicUpdateRecurrence.Hour);


                var connectionProfile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                if (connectionProfile != null)
                {
                    /*FeedDataSource feedDataSource = (FeedDataSource)App.Current.Resources["feedDataSource"];
                    if (feedDataSource != null)
                    {
                        if (feedDataSource.Feeds.Count == 0)
                        {
                            await feedDataSource.GetFeedsAsync();
                        }
                    }*/
                }
                else
                {
                    //TODO: check for cached data and load that, then only warn that you won't get new stuff--best to just say "offline" somewhere in the UI.
                    var messageDialog = new Windows.UI.Popups.MessageDialog("An internet connection is needed to download feeds. Please check your connection and restart the app.");
                    var result = messageDialog.ShowAsync();
                }

                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // Load state from previously suspended and terminated app
                    await kraigb.Common.SuspensionManager.RestoreAsync();
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(SplitPage), args.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Save application state and stop any background activity
            await kraigb.Common.SuspensionManager.SaveAsync();
            
            // Mark the time we were suspeded
            var local = Windows.Storage.ApplicationData.Current.LocalSettings;
            local.Values["suspendTime"] = DateTime.Now.ToString();
            
            deferral.Complete();
        }

        /// <summary>
        /// Invoked when application execution is resumed. The primary purpose of this handler
        /// is to check elapsed time since suspension and update the feed if it's been more than
        /// one hour and there is new content available.
        /// </summary>
        /// <param name="sender">The source of the resume request.</param>
        /// <param name="e">Details about the resume request.</param>
        private async void OnResuming(object sender, object e)
        {
            //TODO: check elapsed time against last feed update

            var local = Windows.Storage.ApplicationData.Current.LocalSettings;

            //Try block just in case suspendTime doesn't exist.
            try
            {
                DateTime suspendTime = DateTime.Parse(local.Values["suspendTime"].ToString());
                DateTime now = DateTime.Now;
                TimeSpan oneHour = new TimeSpan(1, 0, 0);

                //If it's been more than an hour, check if the feed is older
                if (now.Subtract(suspendTime) > oneHour)
                {
                    DateTime lastFeedSave = DateTime.Parse(local.Values["lastFeedSave"].ToString());
                    if (now.Subtract(lastFeedSave) > oneHour)
                    {
                        //TODO: refresh the feed, or at least get the most recent.
                    }
                }
            }

            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Invoked when the application is activated to display search results.
        /// </summary>
        /// <param name="args">Details about the activation request.</param>
        protected async override void OnSearchActivated(Windows.ApplicationModel.Activation.SearchActivatedEventArgs args)
        {
            // Register the Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().QuerySubmitted
            // event in OnWindowCreated to speed up searches once the application is already running
            App.page_title = args.QueryText;

            // If the Window isn't already using Frame navigation, insert our own Frame
            var previousContent = Window.Current.Content;
            var frame = previousContent as Frame;

            // If the app does not contain a top-level frame, it is possible that this 
            // is the initial launch of the app. Typically this method and OnLaunched 
            // in App.xaml.cs can call a common method.
            if (frame == null)
            {
                // Create a Frame to act as the navigation context and associate it with
                // a SuspensionManager key
                frame = new Frame();
                kraigb.Common.SuspensionManager.RegisterFrame(frame, "AppFrame");

                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // Restore the saved session state only when appropriate
                    try
                    {
                        await kraigb.Common.SuspensionManager.RestoreAsync();
                    }
                    catch (kraigb.Common.SuspensionManagerException)
                    {
                        //Something went wrong restoring state.
                        //Assume there is no state and continue
                    }
                }
            }

            frame.Navigate(typeof(SearchResults), args.QueryText);
            Window.Current.Content = frame;

            // Ensure the current window is active
            Window.Current.Activate();
        }
    }
}
