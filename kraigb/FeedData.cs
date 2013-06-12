using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Notifications;

namespace kraigb
{
    public class FeedObject
    {
        public int id { get; set; }
        public string slug { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int post_count { get; set; }
    }

    public class Category : FeedObject
    {
        public int parent { get; set; }
    }

    public class Tag : FeedObject
    {
    }

    public class Comment
    {
        public int id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public string date { get; set; }
        public string content { get; set; }
        public int parent { get; set; }
    }

    public class Post
    {
        public int id { get; set; }
        public string type { get; set; }
        public string slug { get; set; }
        public string url { get; set; }
        public string status { get; set; }
        public string title { get; set; }
        public string title_plain { get; set; }
        public string content { get; set; }
        public string excerpt { get; set; }
        public string date { get; set; }
        public string modified { get; set; }
        public List<Category> categories { get; set; }
        public List<Tag> tags { get; set; }
        public List<Comment> comments { get; set; }
        public List<object> attachments { get; set; }
        public string real_comment_count { get; set; }
        public int comment_count { get; set; }
        public string comment_status { get; set; }
    }

    public class TagListObject
    {
        public string status { get; set; }
        public int count { get; set; }
        public List<Tag> tags { get; set; }
    }

    public class CategoryListObject
    {
        public string status { get; set; }
        public int count { get; set; }
        public List<Category> categories { get; set; }
    }

    public class RootObject
    {
        public string status { get; set; }
        public int count { get; set; }
        public int count_total { get; set; }
        public int pages { get; set; }

        private List<Post> _posts = new List<Post>();
        public List<Post> posts { get { return this._posts; } }
    }

    public class FeedDataSource
    {
        private ObservableCollection<RootObject> _Feeds = new ObservableCollection<RootObject>();
        private CategoryListObject _categoryList = null;
        private TagListObject _tagList = null;

        public ObservableCollection<RootObject> Feeds
        {
            get
            {
                return this._Feeds;
            }
        }

        public CategoryListObject CategoryList
        {
            get 
            {
                if (this._categoryList == null)
                {
                    
                }

                return this._categoryList;
            }
        }

        public TagListObject TagList
        {
            get { return this._tagList; }
        }

        #region Get Feed without any filters
        private async Task<RootObject> GetJsonFeedAsync()
        {
            int post_count = 0;
            HttpClient client = new HttpClient();

            //TODO: save these also in temp state when downloaded otherwise it takes a while to start up
            await this.CacheCategories(client);
            await this.CacheTags(client);

            try
            {
                String response = null;

                response = await LoadFeedFromCache();

                if (response == null)
                {
                    #region Get the number of posts (apparently the JSON API doesn't support retrieving ONLY the post count)

                    // I used this URI because it returns the smallest amount of data
                    Uri post_count_uri = new Uri(App.BLOG_URL + "?json=get_recent_posts&include=id");

                    try
                    {
                        var post_count_response = await client.GetStringAsync(post_count_uri);
                        RootObject post_count_object = await JsonConvert.DeserializeObjectAsync<RootObject>(post_count_response);
                        post_count = post_count_object.count_total;
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                    #endregion

                    Uri feedUri = new Uri(App.BLOG_URL + "?json=get_recent_posts&count=" + post_count);

                    response = await client.GetStringAsync(feedUri);
                    SaveFeedToCache(response);  //Note: no need to wait here as resonse is read-only
                    var local = Windows.Storage.ApplicationData.Current.LocalSettings;
                    local.Values["lastFeedSave"] = DateTime.Now.ToString();
                }

                RootObject postList = await JsonConvert.DeserializeObjectAsync<RootObject>(response);
                RootObject feedData = new RootObject();

                //Update the tile with the most recent posts
                //For the moment, just do the first one.
                Boolean tileUpdated = false;

                foreach (Post post in postList.posts)
                {
                    if (post.title != null)
                    {
                        if (!tileUpdated)
                        {
                            TileUpdater updater = Windows.UI.Notifications.TileUpdateManager.CreateTileUpdaterForApplication();
                            updater.StartPeriodicUpdate(new Uri("http://www.kraigbrockschmidt.com/blog/tileupdate.php"), PeriodicUpdateRecurrence.Hour);
                        }

                        post.title = post.title.Replace("&#8220;", "\"");
                        post.title = post.title.Replace("&#8221;", "\"");
                        post.title = post.title.Replace("&#8243;", "\"");
                        post.title = post.title.Replace("&#8230;", "...");
                        post.title = post.title.Replace("&#038;", "&");
                        post.title = post.title.Replace("&#8217;", "'");
                        post.title = post.title.Replace("&#8211;", "-");
                        post.title = post.title.Replace("&lt;", "<");
                        post.title = post.title.Replace("&gt;", ">");
                    }

                    post.real_comment_count = post.comments.Count.ToString() + " comments";
                    feedData.posts.Add(post);
                }

                return feedData;
            }

            catch (Exception)
            {
                return null;
            }
        }

        public async Task GetFeedsAsync()
        {
            Task<RootObject> feed1 = GetJsonFeedAsync();
            if (Feeds.Count == 0)
                this.Feeds.Add(await feed1);
            else this.Feeds[0] = await feed1;
        }

        private async Task SaveFeedToCache(String response)
        {
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var feedFile = await tempFolder.CreateFileAsync("mainfeed.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(feedFile, response);
        }

        private async Task<string> LoadFeedFromCache()
        {                            
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var local = Windows.Storage.ApplicationData.Current.LocalSettings;
            
            try
            {
                //If the cache is more than 12 hours old <TODO: time interval?>, pretend it doesn't exist.
                DateTime lastFeedSave = DateTime.Parse(local.Values["lastFeedSave"].ToString());

                if (DateTime.Now.Subtract(lastFeedSave) > new TimeSpan(12, 0, 0))
                {
                    return null;
                }

                var feedFile = await tempFolder.GetFileAsync("mainfeed.json");
                var response = await FileIO.ReadTextAsync(feedFile);
                return response;
            }
            catch (Exception)
            {
                return null;
            }
       }


        #endregion

        #region Get Feed with filters applied
        private async Task<RootObject> GetJsonFeedAsync(string filter, string index)
        {
            int post_count = 0;
            System.Net.Http.HttpClient client = new HttpClient();

            #region Get the number of posts (apparently the JSON API doesn't support retrieving ONLY the post count)

            Uri post_count_uri;
            if (filter == "category")
                post_count_uri = new Uri(App.BLOG_URL + "?json=get_category_posts&include=id&slug=" + index);
            else if (filter == "tag")
                post_count_uri = new Uri(App.BLOG_URL + "?json=get_tag_posts&include=id&slug=" + index);
            else
                post_count_uri = new Uri(App.BLOG_URL + "?json=get_search_results&include=id&search=" + index);

            try
            {
                var post_count_response = await client.GetStringAsync(post_count_uri);
                RootObject post_count_object = await JsonConvert.DeserializeObjectAsync<RootObject>(post_count_response);
                post_count = post_count_object.count_total;
            }
            catch (Exception)
            {
                return null;
            }

            #endregion

            Uri feedUri;
            if (filter == "category")
                feedUri = new Uri(App.BLOG_URL + "?json=get_category_posts&slug=" + index + "&count=" + post_count);
            else if (filter == "tag")
                feedUri = new Uri(App.BLOG_URL + "?json=get_tag_posts&slug=" + index + "&count=" + post_count);
            else
                feedUri = new Uri(App.BLOG_URL + "?json=get_search_results&search=" + index + "&count=" + post_count);

            try
            {
                var response = await client.GetStringAsync(feedUri);
                RootObject postList = await JsonConvert.DeserializeObjectAsync<RootObject>(response);
                RootObject feedData = new RootObject();

                foreach (Post post in postList.posts)
                {
                    if (post.title != null)
                    {
                        post.title = post.title.Replace("&#8220;", "\"");
                        post.title = post.title.Replace("&#8221;", "\"");
                        post.title = post.title.Replace("&#8243;", "\"");
                        post.title = post.title.Replace("&#8230;", "...");
                        post.title = post.title.Replace("&#038;", "&");
                        post.title = post.title.Replace("&#8217;", "'");
                        post.title = post.title.Replace("&#8211;", "-");
                        post.title = post.title.Replace("&lt;", "<");
                        post.title = post.title.Replace("&gt;", ">");
                    }

                    post.real_comment_count = post.comments.Count.ToString() + " comments";
                    feedData.posts.Add(post);
                }
                return feedData;
            }

            catch (Exception)
            {
                return null;
            }
        }

        public async Task GetFeedsAsync(string filter, string index)
        {
            Task<RootObject> feed1 = GetJsonFeedAsync(filter, index);
            if (Feeds.Count == 0)
                this.Feeds.Add(await feed1);
            else this.Feeds[0] = await feed1;
        }
        #endregion

        public static RootObject GetFeed()
        {
            var _feedDataSource = App.Current.Resources["feedDataSource"] as FeedDataSource;
            return _feedDataSource.Feeds.First();
        }

        public static Post GetItem(string uniqueId)
        {
            var _feedDataSource = App.Current.Resources["feedDataSource"] as FeedDataSource;
            var matches = _feedDataSource.Feeds
                .Where(group => group != null)
                .SelectMany(group => group.posts).Where((item) => item.title.Equals(uniqueId));
            if (matches.Count() == 1) return matches.First();
            return null;            
        }
        
        public async Task<Boolean> CacheCategories(HttpClient client, Boolean forceReload = false)
        {
            //If we've already loaded categories, don't bother again (TODO: refresh strategy)
            if (this._categoryList != null && !forceReload)
            {
                return true;
            }

            StorageFolder tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
            String filename = "categories.json";
            String category_response = null;

            //Attempt to load categories from the saved file
            if (!forceReload)
            {
                try
                {
                    StorageFile file = await tempFolder.GetFileAsync(filename);
                    category_response = await FileIO.ReadTextAsync(file);
                }

                catch (Exception)
                {
                }
            }


            //Failing that, go load them
            Uri category_index_uri = new Uri(App.BLOG_URL + "?json=get_category_index");

            try
            {
                //If category_response is still null, we are either forcing reload or had an error
                if (category_response == null)
                {
                    category_response = await client.GetStringAsync(category_index_uri);
                }

                this._categoryList = await JsonConvert.DeserializeObjectAsync<CategoryListObject>(category_response);
                StorageFile file = await tempFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, category_response);
                return true;
            }

            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Boolean> CacheTags(HttpClient client, Boolean forceReload = false) 
        {
            if (this._tagList != null && !forceReload)
            {
                return true;
            }

            StorageFolder tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
            String filename = "tags.json";
            String tag_response = null;

            //Attempt to load tags from the saved file
            if (!forceReload)
            {
                try
                {
                    StorageFile file = await tempFolder.GetFileAsync(filename);
                    tag_response = await FileIO.ReadTextAsync(file);
                }

                catch (Exception)
                {
                }
            }

            Uri tag_index_uri = new Uri(App.BLOG_URL + "?json=get_tag_index");

            try
            {
                //If tag_response is still null, we are either forcing reload or had an error
                if (tag_response == null)
                {
                    tag_response = await client.GetStringAsync(tag_index_uri);
                }

                this._tagList = await JsonConvert.DeserializeObjectAsync<TagListObject>(tag_response);
                StorageFile file = await tempFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, tag_response);

                return true;
            }

            catch (Exception)
            {
                return false;
            }

        }

    }
}
