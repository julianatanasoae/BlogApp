using Callisto.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace kraigb
{
    public sealed partial class SplitPage : kraigb.Common.LayoutAwarePage
    {
        const string style = "<style type=\"text/css\"> p {font-family:'Segoe UI'; } h1 {font-family:'Segoe UI';} h2 {font-family:'Segoe UI';} h3 {font-family:'Segoe UI';} h4 {font-family:'Segoe UI';} blockquote {font-family: 'Segoe UI'; font-style:italic;} a:link {font-family:'Segoe UI'; } li { font-family: 'Segoe UI'; } </style> ";

        public SplitPage()
        {
            this.InitializeComponent();
            ShareSourceLoad();
        }

        public void ShareSourceLoad()
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(this.DataRequested);
        }

        async void DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (itemListView.SelectedItem != null)
            {
                Post selectedItem = itemListView.SelectedItem as Post;
                args.Request.Data.Properties.Title = selectedItem.title;
                args.Request.Data.Properties.Description = Windows.Data.Html.HtmlUtilities.ConvertToText(selectedItem.content).ToString().Substring(0, 140);
                args.Request.Data.SetUri(new Uri(selectedItem.url));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            DataTransferManager.GetForCurrentView().DataRequested -= new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(this.DataRequested);
        }

        #region Page state management

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected async override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            progressRing.Visibility = Visibility.Visible;
            //TODO: restore this later on this.RegisterBackgroundTask();
            Windows.UI.Xaml.Media.Animation.Storyboard sb =
                this.FindName("PopInStoryBoard") as Windows.UI.Xaml.Media.Animation.Storyboard;
            if (sb != null) sb.Begin();

            // TODO: Assign a bindable group to this.DefaultViewModel["Group"]
            // TODO: Assign a collection of bindable items to this.DefaultViewModel["Items"]
            FeedDataSource feedDataSource = (FeedDataSource)App.Current.Resources["feedDataSource"];
            if (feedDataSource != null)
            {
                await feedDataSource.GetFeedsAsync();
            }
            RootObject feedData = FeedDataSource.GetFeed();
            if (feedData != null)
            {
                this.DefaultViewModel["Feed"] = feedData;
                this.DefaultViewModel["Items"] = feedData.posts;
            }

            if (pageState == null)
            {
                // When this is a new page, select the first item automatically unless logical page
                // navigation is being used (see the logical page navigation #region below.)
                if (!this.UsingLogicalPageNavigation() && this.itemsViewSource.View != null)
                {
                    this.itemsViewSource.View.MoveCurrentToFirst();
                }
                else
                {
                    //this.itemsViewSource.View.MoveCurrentToPosition(-1);
                }
            }
            else
            {
                // Restore the previously saved state associated with this page
                if (pageState.ContainsKey("SelectedItem") && this.itemsViewSource.View != null)
                {
                    // TODO: Invoke this.itemsViewSource.View.MoveCurrentTo() with the selected
                    //       item as specified by the value of pageState["SelectedItem"]
                    string itemTitle = (string)pageState["SelectedItem"];
                    Post selectedItem = FeedDataSource.GetItem(itemTitle);
                    this.itemsViewSource.View.MoveCurrentTo(selectedItem);
                }
            }
            progressRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
            if (this.itemsViewSource.View != null)
            {
                var selectedItem = this.itemsViewSource.View.CurrentItem;
                // TODO: Derive a serializable navigation parameter and assign it to
                //       pageState["SelectedItem"]
                if (selectedItem != null)
                {
                    string itemTitle = ((Post)selectedItem).title;
                    pageState["SelectedItem"] = itemTitle;
                }
            }
        }

        #endregion

        #region Logical page navigation

        // Visual state management typically reflects the four application view states directly
        // (full screen landscape and portrait plus snapped and filled views.)  The split page is
        // designed so that the snapped and portrait view states each have two distinct sub-states:
        // either the item list or the details are displayed, but not both at the same time.
        //
        // This is all implemented with a single physical page that can represent two logical
        // pages.  The code below achieves this goal without making the user aware of the
        // distinction.

        /// <summary>
        /// Invoked to determine whether the page should act as one logical page or two.
        /// </summary>
        /// <param name="viewState">The view state for which the question is being posed, or null
        /// for the current view state.  This parameter is optional with null as the default
        /// value.</param>
        /// <returns>True when the view state in question is portrait or snapped, false
        /// otherwise.</returns>
        private bool UsingLogicalPageNavigation(ApplicationViewState? viewState = null)
        {
            if (viewState == null) viewState = ApplicationView.Value;
            return viewState == ApplicationViewState.FullScreenPortrait ||
                viewState == ApplicationViewState.Snapped;
        }

        /// <summary>
        /// Invoked when an item within the list is selected.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is Snapped)
        /// displaying the selected item.</param>
        /// <param name="e">Event data that describes how the selection was changed.</param>
        void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invalidate the view state when logical page navigation is in effect, as a change
            // in selection may cause a corresponding change in the current logical page.  When
            // an item is selected this has the effect of changing from displaying the item list
            // to showing the selected item's details.  When the selection is cleared this has the
            // opposite effect.
            if (this.UsingLogicalPageNavigation()) this.InvalidateVisualState();
            Selector list = sender as Selector;
            Post selectedItem = list.SelectedItem as Post;
            if (selectedItem != null)
            {
                selectedItem.content = AddScriptToLink(selectedItem.content);
                this.contentView.NavigateToString(style + selectedItem.content);
            }
            else
            {
                this.contentView.NavigateToString("");
            }
        }

        /// <summary>
        /// Invoked when the page's back button is pressed.
        /// </summary>
        /// <param name="sender">The back button instance.</param>
        /// <param name="e">Event data that describes how the back button was clicked.</param>
        protected override void GoBack(object sender, RoutedEventArgs e)
        {
            if (this.UsingLogicalPageNavigation() && itemListView.SelectedItem != null)
            {
                // When logical page navigation is in effect and there's a selected item that
                // item's details are currently displayed.  Clearing the selection will return to
                // the item list.  From the user's point of view this is a logical backward
                // navigation.
                this.itemListView.SelectedItem = null;
            }
            else
            {
                // When logical page navigation is not in effect, or when there is no selected
                // item, use the default back button behavior.
                base.GoBack(sender, e);
            }
        }

        /// <summary>
        /// Invoked to determine the name of the visual state that corresponds to an application
        /// view state.
        /// </summary>
        /// <param name="viewState">The view state for which the question is being posed.</param>
        /// <returns>The name of the desired visual state.  This is the same as the name of the
        /// view state except when there is a selected item in portrait and snapped views where
        /// this additional logical page is represented by adding a suffix of _Detail.</returns>
        protected override string DetermineVisualState(ApplicationViewState viewState)
        {
            // Update the back button's enabled state when the view state changes
            var logicalPageBack = this.UsingLogicalPageNavigation(viewState) && this.itemListView.SelectedItem != null;
            var physicalPageBack = this.Frame != null && this.Frame.CanGoBack;
            this.DefaultViewModel["CanGoBack"] = logicalPageBack || physicalPageBack;

            // Determine visual states for landscape layouts based not on the view state, but
            // on the width of the window.  This page has one layout that is appropriate for
            // 1366 virtual pixels or wider, and another for narrower displays or when a snapped
            // application reduces the horizontal space available to less than 1366.
            if (viewState == ApplicationViewState.Filled ||
                viewState == ApplicationViewState.FullScreenLandscape)
            {
                var windowWidth = Window.Current.Bounds.Width;
                if (windowWidth >= 1366) return "FullScreenLandscapeOrWide";
                return "FilledOrNarrow";
            }

            // When in portrait or snapped start with the default visual state name, then add a
            // suffix when viewing details instead of the list
            var defaultStateName = base.DetermineVisualState(viewState);
            return logicalPageBack ? defaultStateName + "_Detail" : defaultStateName;
        }

        #endregion

        private void ContentView_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            string errorString = "<p>Page could not be loaded.</p><p>Error is: " + e.WebErrorStatus.ToString() + "</p>";
            this.contentView.NavigateToString(errorString);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadState("", null);
        }

        private void ViewCommentsPage_Click(object sender, RoutedEventArgs e)
        {
            Post selectedItem = this.itemListView.SelectedItem as Post;
            if (selectedItem != null && this.Frame != null)
            {
                string itemTitle = selectedItem.title;
                this.Frame.Navigate(typeof(CommentsPage), itemTitle);
            }
        }

        private void AppBar_Opened(object sender, object e)
        {
            WebViewBrush wvb = new WebViewBrush();
            wvb.SourceName = "contentView";
            wvb.Redraw();
            contentViewRect.Fill = wvb;
            contentView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void AppBar_Closed(object sender, object e)
        {
            contentView.Visibility = Windows.UI.Xaml.Visibility.Visible;
            contentViewRect.Fill = new SolidColorBrush(Windows.UI.Colors.Transparent);
        }

        public static string AddScriptToLink(string text)
        {
            const string hrefScript = " onclick=\"window.external.notify('{0}'); return false;\" ";
            const string pattern = @"href=\""(.*?)\""";

            var result = text;
            var matches = Regex.Matches(text, pattern);
            var sortedMatches = matches.Cast<Match>().OrderByDescending(x => x.Index);
            foreach (var match in sortedMatches)
            {
                var replacement = string.Format(hrefScript, match.Groups[1].Value);
                result = result.Insert(match.Index, replacement);
            }
            return result;
        }

        private async void contentView_ScriptNotify(object sender, NotifyEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(e.Value));
        }

        #region Filtering

        public class NavigateToFilterTypeCommand : ICommand
        {
            private object sender;
            private string type;
            public NavigateToFilterTypeCommand(object sender, string type)
            {
                this.sender = sender;
                this.type = type;
            }

            public async void Execute(object param)
            {
                FeedDataSource feedDataSource = (FeedDataSource)App.Current.Resources["feedDataSource"];
                if (feedDataSource == null)
                {
                    return;
                }
            
                //Shouldn't need this now, but I'll leave it here
                ProgressRing filter_ring = new ProgressRing();
                filter_ring.IsActive = true;
                Flyout flyout = new Flyout();
                flyout.PlacementTarget = sender as UIElement;
                flyout.Placement = PlacementMode.Top;
                flyout.HostMargin = new Thickness(0);
                Border b = new Border();
                b.Width = 20;
                b.Height = 20;
                b.Child = filter_ring;
                flyout.Content = b;
                flyout.IsOpen = true;

                Menu menu = new Menu();
                menu.MaxHeight = 300;

                switch (type)
                {
                    case "category":
                        foreach (Category item in feedDataSource.CategoryList.categories)
                            {
                                ToggleMenuItem menuItem = new ToggleMenuItem();
                                menuItem.Text = item.title;
                                menuItem.Command = new MenuCategoryCommand(item.slug, item.title);
                                menu.Items.Add(menuItem);
                            }
                        break;

                    case "tag":
                    default:
                        foreach (Tag item in feedDataSource.TagList.tags)
                        {
                            ToggleMenuItem menuItem = new ToggleMenuItem();
                            menuItem.Text = item.title;
                            menuItem.Command = new MenuTagCommand(item.slug, item.title);
                            menu.Items.Add(menuItem);
                        }
                        break;
                }

                flyout.Content = menu;
            }

            public bool CanExecute(object param)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;
        }

        public class MenuTagCommand : ICommand
        {
            private string myTag;
            private string tagTitle;
            public MenuTagCommand(string myTag, string tagTitle)
            {
                this.myTag = myTag;
                this.tagTitle = tagTitle;
            }

            public void Execute(object param)
            {
                App.page_title = tagTitle;
                var frame = new Frame();
                frame.Navigate(typeof(SplitPageFilteredByTag), myTag);
                Window.Current.Content = frame;
            }

            public bool CanExecute(object param)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;
        }

        public class MenuCategoryCommand : ICommand
        {
            private string myCategory;
            private string categoryTitle;
            public MenuCategoryCommand(string myCategory, string categoryTitle)
            {
                this.myCategory = myCategory;
                this.categoryTitle = categoryTitle;
            }

            public void Execute(object param)
            {
                App.page_title = categoryTitle;
                var frame = new Frame();
                frame.Navigate(typeof(SplitPageFilteredByCategory), myCategory);
                Window.Current.Content = frame;
            }

            public bool CanExecute(object param)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(this.DataRequested);
            Flyout flyout = new Flyout();
            flyout.PlacementTarget = sender as UIElement;
            flyout.Placement = PlacementMode.Top;
            flyout.HostMargin = new Thickness(0);

            Menu menu = new Menu();

            ToggleMenuItem filterByTag_menuItem = new ToggleMenuItem();
            filterByTag_menuItem.Text = "by Tag";
            filterByTag_menuItem.Command = new NavigateToFilterTypeCommand(sender, "tag");
            menu.Items.Add(filterByTag_menuItem);

            ToggleMenuItem filterByCategory_menuItem = new ToggleMenuItem();
            filterByCategory_menuItem.Text = "by Category";
            filterByCategory_menuItem.Command = new NavigateToFilterTypeCommand(sender, "category");
            menu.Items.Add(filterByCategory_menuItem);

            menu.MaxHeight = 300;

            flyout.Content = menu;
            flyout.IsOpen = true;

            UpdateLayout();
        }

        #endregion

    }
}
