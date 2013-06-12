using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace kraigb
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class CommentsPage : kraigb.Common.LayoutAwarePage
    {
        int post_id = 0;
        string post_title = "";
        public CommentsPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            string itemTitle = (string)navigationParameter;
            post_title = itemTitle;
            Post feedItem = FeedDataSource.GetItem(itemTitle);
            post_id = feedItem.id;
            if (feedItem != null)
            {
                this.DataContext = feedItem;
                foreach (Comment comment in feedItem.comments)
                {
                    HtmlDocument html = new HtmlDocument();
                    html.LoadHtml(comment.content);
                    comment.content = HtmlEntity.DeEntitize(html.DocumentNode.InnerText);
                }
                this.commentsListView.ItemsSource = feedItem.comments;
            }
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
        }

        public class ServerResponse
        {
            public string status { get; set; }
            public string error { get; set; }
        }

        private async void postCommentButton_Click(object sender, RoutedEventArgs e)
        {
            progressRing.Visibility = Visibility.Visible;
            var name = NameText.Text;
            var email = MailText.Text;
            var content = CommentText.Text;

            string template = "post_id={0}&name={1}&email={2}&content={3}";
            string postData = string.Format(template, post_id, name, email, content);
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            WebRequest request = HttpWebRequest.Create(App.BLOG_URL + "?json=submit_comment");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            Stream dataStream = await request.GetRequestStreamAsync();
            await dataStream.WriteAsync(byteArray, 0, byteArray.Length);
            try
            {
                WebResponse response = await request.GetResponseAsync();
                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = await reader.ReadToEndAsync();
                var deserialized_response = JsonConvert.DeserializeObject<ServerResponse>(responseFromServer);
                if (deserialized_response.status != "error")
                {
                    StatusBlock.Foreground = new SolidColorBrush(Colors.Green);
                    StatusBlock.Text = "Comment posted!";
                    progressRing.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatusBlock.Foreground = new SolidColorBrush(Colors.Red);
                    StatusBlock.Text = deserialized_response.error;
                    progressRing.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusBlock.Text = "An error occurred. Please try again.";
                progressRing.Visibility = Visibility.Collapsed;
            }
            FeedDataSource feedDataSource = (FeedDataSource)App.Current.Resources["feedDataSource"];
            if (feedDataSource != null)
            {
                await feedDataSource.GetFeedsAsync();
            }
            if (post_title != null)
            {
                LoadState(post_title, null);
            }
        }
    }
}
