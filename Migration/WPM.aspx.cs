using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Telerik.Sitefinity;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Telerik.Sitefinity.Blogs.Model;
using Telerik.Sitefinity.Workflow;
using CsQuery;
using Telerik.Sitefinity.GenericContent.Model;
using Telerik.Sitefinity.Modules.Libraries;
using Telerik.Sitefinity.Modules.Libraries.Configuration;
using System.IO;
using Telerik.Sitefinity.Libraries.Model;
using System.Net;
using System.Drawing.Imaging;
using Telerik.Sitefinity.Taxonomies;
using Telerik.Sitefinity.Taxonomies.Model;
using Telerik.Sitefinity.Modules.Blogs;
using System.Web.Services;
using System.Web.Script.Services;
using Telerik.Sitefinity.Model;
using Telerik.Sitefinity.Security;
using Telerik.Sitefinity.Security.Model;
using Telerik.Sitefinity.Services;
using System.Threading;
using Telerik.Sitefinity.Web.UI;
using Telerik.Sitefinity.Services.Comments;
using Telerik.Sitefinity.Services.Comments.Proxies;
using Telerik.Sitefinity.RelatedData;

namespace SitefinityWebApp.Migrate
{
    public partial class WPM : System.Web.UI.Page
    {
        private FlatTaxonomy _tags = SFTaxonomyManager.GetTaxonomies<FlatTaxonomy>().Where(t => t.Name == "Tags").SingleOrDefault();
        private HierarchicalTaxonomy _categories = SFTaxonomyManager.GetTaxonomies<HierarchicalTaxonomy>().Where(t => t.Name == "Categories").SingleOrDefault();

        private string _fromFormat = "yyyy/MM/dd HH:MM:ss";

        private XNamespace excerpt = "http://wordpress.org/export/1.2/excerpt/";
        private XNamespace content = "http://purl.org/rss/1.0/modules/content/";
        private XNamespace wfw = "http://wellformedweb.org/CommentAPI/";
        private XNamespace dc = "http://purl.org/dc/elements/1.1/";
        private XNamespace wp = "http://wordpress.org/export/1.2/";
        
        // not hooked to UI :/
        // this allows the import to split authors into different blogs
        // would probably need to come AFTER the file is uploaded...
        private bool _useParentBlogMap;
        private Dictionary<string, string> parentBlogMap = new Dictionary<string, string>()
        {
            {"author1@somedomain.com", "Main Blog"},
            {"author2@somedomain.com", "Main Blog"},
            {"author3@somedomain.com", "Some Other Blog"},
            {"author4@somedomain.com", "Yet Another Blog"},
            {"author5@somedomain.com", "You Get the Idea"}
        };

        private string _importLog;
        private Guid _targetBlogId = Guid.Empty;
        private List<PostAuthor> _authors = new List<PostAuthor>();
        private List<GenericBlogPost> _posts = new List<GenericBlogPost>();
 
        protected void Page_Load(object sender, EventArgs e)
        {
            Server.ScriptTimeout = 1000;

            if (!((Page)System.Web.HttpContext.Current.CurrentHandler).IsPostBack)
            {
                var blogs = App.WorkWith().Blogs().Get().ToList();

                TargetBlog.DataValueField = "Id";
                TargetBlog.DataTextField = "Title";
                TargetBlog.DataSource = blogs;
                TargetBlog.DataBind();
            }
        }
 
        protected void Submit_Click(object sender, EventArgs e)
        {
            if (TargetBlog.SelectedValue == "")
            {
                return;
            }

            if (!WordPressFile.HasFile)
            {
                return;
            }

            var xml = XDocument.Load(WordPressFile.FileContent);

            _authors = GetPostAuthorsFromXML(xml);
            _posts = GetPostsFromXML(xml);

            foreach (var post in _posts)
            {
                PostAuthor author = _authors.Where(a => a.Username == post.Creator).FirstOrDefault();

                string parentBlogTitle = "";

                if (_useParentBlogMap)
                {
                    parentBlogTitle = parentBlogMap[author.Email];
                    _targetBlogId = SFBlogsManager.GetBlogs().Where(b => b.Title == parentBlogMap[author.Email]).SingleOrDefault().Id;
                }
                else
                {
                    parentBlogTitle = TargetBlog.SelectedItem.Text;
                    _targetBlogId = Guid.Parse(TargetBlog.SelectedValue);
                }

                if (BlogExists(_targetBlogId))
                {
                    ImportPost(_targetBlogId, post);
                }
                else
                {
                    _importLog += "<br>Parent blog '" + parentBlogTitle + "' not found for post '" + post.Title + "'.<br>";
                }
            }
        }

        private List<PostAuthor> GetPostAuthorsFromXML(XDocument xml)
        {
            var authors = xml.Descendants(wp + "author")
                .Select(a => new PostAuthor
                {
                    Id = Int32.Parse(a.Element(wp + "author_id").Value),
                    Email = a.Element(wp + "author_email").Value,
                    Username = a.Element(wp + "author_login").Value,
                    FirstName = a.Element(wp + "author_first_name").Value,
                    LastName = a.Element(wp + "author_last_name").Value,
                    Nickname = a.Element(wp + "author_display_name").Value
                }).ToList();

            return authors;
        }

        private List<GenericBlogPost> GetPostsFromXML(XDocument xml)
        {
            var xmlPosts = xml.Descendants("item")
                .Where(t => t.Element(wp + "post_type").Value == "post")
                .Where(t => t.Element(wp + "status").Value == "publish");

            var posts = new List<GenericBlogPost>();

            foreach (XElement p in xmlPosts)
            {
                GenericBlogPost post = new GenericBlogPost();

                post.Creator = p.Element(dc + "creator").Value;
                post.Title = p.Element("title").Value;
                post.AttachmentUrl = (string)p.Element(wp + "attachment_url") ?? String.Empty;
                post.PostName = p.Element(wp + "post_name").Value;
                post.Link = p.Element("link").Value;
                post.PublicationDate = DateTime.Parse(p.Element("pubDate").Value);
                post.Content = p.Element(content + "encoded").Value;
                post.AllowComments = (p.Element(wp + "comment_status").Value == "open") ? true : false;
                post.Tags = p.Elements("category").Where(b => (string)b.Attribute("domain") == "post_tag").Select(c => c.Value).ToList();
                post.Categories = p.Elements("category").Where(b => (string)b.Attribute("domain") == "category").Select(c => c.Value).ToList();
                post.Meta = p.Elements(wp + "postmeta").Count() > 0 ?
                    p.Elements(wp + "postmeta")
                    .Where(x => x.Element(wp + "meta_key").Value != "enclosure")
                    .ToDictionary(
                        m => m.Element(wp + "meta_key").Value,
                        m => m.Element(wp + "meta_value").Value
                    ) : null;
                post.Comments = (p.Elements(wp + "comment").Count() > 0 ? p.Elements(wp + "comment")
                    .Select(c => new GenericPostComment
                    {
                        Author = c.Element(wp + "comment_author").Value,
                        AuthorEmail = c.Element(wp + "comment_author_email").Value,
                        AuthorIP = c.Element(wp + "comment_author_IP").Value,
                        AuthorURL = c.Element(wp + "comment_author_url").Value,
                        CommentDate = DateTime.Parse(c.Element(wp + "comment_date").Value),
                        CommentDateGMT = DateTime.Parse(c.Element(wp + "comment_date_gmt").Value),
                        Content = c.Element(wp + "comment_content").Value,
                        Status = (c.Element(wp + "comment_approved").Value == "1") ? StatusConstants.Published : StatusConstants.WaitingForApproval,
                        Type = c.Element(wp + "comment_type").Value,
                        Parent = int.Parse(c.Element(wp + "comment_parent").Value),
                        UserId = int.Parse(c.Element(wp + "comment_user_id").Value)
                    }).ToArray() : null);
                
                posts.Add(post);
            }

            return posts;
        }

        private void ImportPost(Guid blogId, GenericBlogPost post)
        {
            Guid masterBlogPostId = Guid.NewGuid();

            BlogPost blogPost = SFBlogsManager.CreateBlogPost(masterBlogPostId);

            Blog blog = SFBlogsManager.GetBlogs().Where(b => b.Id == blogId).SingleOrDefault();

            blogPost.Parent = blog;

            masterBlogPostId = blogPost.Id;

            blogPost.Title = post.Title;
            blogPost.DateCreated = post.DateCreated;
            blogPost.PublicationDate = post.PublicationDate;
            blogPost.LastModified = post.PublicationDate;

            string content = ParseContent(post.Content);

            content = ImportAndLinkContentImages(content, post.Title, blog.Title);

            blogPost.Content = content;

            blogPost.Summary = post.Summary;
            blogPost.AllowComments = post.AllowComments;
            blogPost.UrlName = post.PostName ?? Regex.Replace(post.Title.ToLower(), @"[^\w\-\!\$\'\(\)\=\@\d_]+", "-");
            blogPost.Owner = Guid.Empty;

            PostAuthor author = _authors.Where(a => a.Username == post.Creator).FirstOrDefault();

            if (author != null)
            {
                User user = SFUserManager.GetUserByEmail(author.Email);

                if (user != null)
                {
                    blogPost.Owner = user.Id;
                }
            }

            if (ddlCategoriesImportMode.SelectedIndex > 0)
            {
                if (post.Categories != null && post.Categories.Count > 0)
                {
                    foreach (string category in post.Categories)
                    {
                        HierarchicalTaxon taxon;

                        taxon = SFTaxonomyManager.GetTaxa<HierarchicalTaxon>().Where(t => t.Name == category).FirstOrDefault();

                        if (taxon == null && ddlCategoriesImportMode.SelectedIndex > 1)
                        {
                            taxon = CreateCategory(category);
                        }

                        if (taxon != null)
                        {
                            blogPost.Organizer.AddTaxa("Category", taxon.Id);
                        }
                    }
                }
            }

            if (ddlTagsImportMode.SelectedIndex > 0)
            {
                if (post.Tags != null && post.Tags.Count > 0)
                {
                    foreach (string tag in post.Tags)
                    {
                        FlatTaxon taxon;

                        taxon = SFTaxonomyManager.GetTaxa<FlatTaxon>().Where(t => t.Name == tag).FirstOrDefault();

                        if (taxon == null && ddlTagsImportMode.SelectedIndex > 1)
                        {
                            taxon = CreateTag(tag);
                        }

                        if (taxon != null)
                        {
                            blogPost.Organizer.AddTaxa("Tags", taxon.Id);
                        }
                    }
                }
            }

            if (post.AttachmentUrl != String.Empty)
            {
                string imageSrc = post.AttachmentUrl;

                string imgFile = Path.GetFileName(imageSrc);

                var sfImages = App.WorkWith().Images().Where(i => i.Status == ContentLifecycleStatus.Master).Get().ToList();

                var sfImg = sfImages.Where(i => Path.GetFileName(i.FilePath) == imgFile.ToLower()).FirstOrDefault();

                if (sfImg != null)
                {
                    blogPost.CreateRelation(sfImg, "Thumbnail");
                }
                else
                {
                    if (ImportImage(imageSrc, post.Title, blog.Title))
                    {
                        sfImg = App.WorkWith().Images().Where(i => i.Status == ContentLifecycleStatus.Master).Get().ToList().Where(i => Path.GetFileName(i.FilePath) == imgFile.ToLower()).FirstOrDefault();

                        blogPost.CreateRelation(sfImg, "Thumbnail");
                    }
                }
            }

            if (post.Meta != null)
            {
                if (post.Meta.ContainsKey("_yoast_wpseo_title"))
                {
                    blogPost.SetValue("SEOTitle", post.Meta["_yoast_wpseo_title"]);
                }

                if (post.Meta.ContainsKey("_yoast_wpseo_metadesc"))
                {
                    blogPost.SetValue("SEOMetaDescription", post.Meta["_yoast_wpseo_metadesc"]);
                }

                if (post.Meta.ContainsKey("_yoast_wpseo_focuskw"))
                {
                    blogPost.SetValue("SEOMetaKeywords", post.Meta["_yoast_wpseo_focuskw"]);
                }
            }

            SFBlogsManager.RecompileItemUrls(blogPost);
            SFBlogsManager.Lifecycle.PublishWithSpecificDate(blogPost, post.PublicationDate);
            SFBlogsManager.SaveChanges();

            var bag = new Dictionary<string, string>();
            bag.Add("ContentType", typeof(BlogPost).FullName);
            WorkflowManager.MessageWorkflow(masterBlogPostId, typeof(BlogPost), null, "Publish", false, bag);

            if (chkImportComments.Checked)
            {
                if (post.Comments != null && post.Comments.Count() > 0)
                {
                    foreach (var comment in post.Comments)
                    {
                        CreateBlogPostComment(masterBlogPostId, comment);
                    }
                }
            }

            var dbg = _importLog;
        }

        #region Comment Stuff
        public static void CreateBlogPostComment(Guid blogPostId, GenericPostComment comment)
        {
            BlogPost blogPost = SFBlogsManager.GetBlogPost(blogPostId);

            var cs = SystemManager.GetCommentsService();
            var language = Thread.CurrentThread.CurrentUICulture.Name;
            var threadKey = ControlUtilities.GetLocalizedKey(blogPostId, language);

            var commentAuthor = new AuthorProxy(comment.Author, comment.AuthorEmail);

            EnsureBlogPostThreadExists(threadKey, commentAuthor, blogPost.Title, SFBlogsManager, language, cs);

            var commentProxy = new CommentProxy(comment.Content, threadKey, commentAuthor, comment.AuthorIP);

            commentProxy.Status = StatusConstants.Published;

            var newComment = cs.CreateComment(commentProxy);

            newComment.DateCreated = comment.CommentDateGMT;
            newComment.LastModified = comment.CommentDateGMT;

            cs.UpdateComment(newComment);
        }

        private static void EnsureBlogPostThreadExists(string threadKey, IAuthor author, string threadTitle, BlogsManager manager, string language, ICommentService cs)
        {
            ThreadFilter threadFilter = new ThreadFilter();
            threadFilter.ThreadKey.Add(threadKey);
            var thread = cs.GetThreads(threadFilter).SingleOrDefault();

            if (thread == null)
            {
                var groupKey = ControlUtilities.GetUniqueProviderKey(typeof(BlogsManager).FullName, manager.Provider.Name);

                EnsureBlogPostGroupExists(groupKey, author, cs);

                var threadProxy = new ThreadProxy(threadTitle, typeof(BlogPost).FullName, groupKey, author) { Language = language, Key = threadKey };
                thread = cs.CreateThread(threadProxy);
            }
        }

        private static void EnsureBlogPostGroupExists(string groupKey, IAuthor author, ICommentService cs)
        {
            GroupFilter groupFilter = new GroupFilter();
            groupFilter.GroupKey.Add(groupKey);
            var group = cs.GetGroups(groupFilter).SingleOrDefault();

            if (group == null)
            {
                var groupProxy = new GroupProxy("Group title", "blog posts in provider", author) { Key = groupKey };
                group = cs.CreateGroup(groupProxy);
            }
        }
        #endregion

        #region Blog Stuff
        private bool BlogExists(Guid blogId)
        {
            Blog parentBlog = App.WorkWith().Blogs().Where(b => b.Id == blogId).Get().FirstOrDefault();

            if (parentBlog != null)
            {
                return true;
            }

            return false;
        }
        #endregion

        #region Taxonomy Stuff
        private FlatTaxon CreateTag(string tagName)
        {
            FlatTaxon taxon = SFTaxonomyManager.CreateTaxon<FlatTaxon>();
            taxon.Name = tagName;
            taxon.Title = tagName;
            taxon.UrlName = Regex.Replace(tagName.ToLower(), @"[^\w\-\!\$\'\(\)\=\@\d_]+", "-");

            _tags.Taxa.Add(taxon);
            SFTaxonomyManager.SaveChanges();

            return taxon;
        }

        private HierarchicalTaxon CreateCategory(string categoryName)
        {
            HierarchicalTaxon taxon = SFTaxonomyManager.CreateTaxon<HierarchicalTaxon>();
            taxon.Name = categoryName;
            taxon.Title = categoryName;
            taxon.UrlName = Regex.Replace(categoryName.ToLower(), @"[^\w\-\!\$\'\(\)\=\@\d_]+", "-");

            _categories.Taxa.Add(taxon);
            SFTaxonomyManager.SaveChanges();

            return taxon;
        }
        #endregion

        #region Album Stuff
        private bool CreateAlbum(string albumTitle)
        {
            App.WorkWith().Album().CreateNew(Guid.NewGuid())
            .Do(b =>
            {
                b.Title = albumTitle;
                b.DateCreated = DateTime.UtcNow;
                b.LastModified = DateTime.UtcNow;
                b.UrlName = Regex.Replace(albumTitle.ToLower(), @"[^\w\-\!\$\'\(\)\=\@\d_]+", "-");
            })
            .SaveChanges();

            return true;
        }
        #endregion

        #region Image Stuff
        private bool CreateImage(string albumTitle, string imageTitle, Stream remoteImageStream, string imageExtension, string altText)
        {
            Telerik.Sitefinity.Libraries.Model.Image image = SFLibrariesManager.GetImages().Where(i => i.Title == imageTitle).FirstOrDefault();

            if (image == null)
            {
                Guid masterImageId = Guid.NewGuid();
                image = SFLibrariesManager.CreateImage(masterImageId);

                Album album = SFLibrariesManager.GetAlbums().Where(a => a.Title == albumTitle).FirstOrDefault();

                if (album != null)
                {
                    image.Parent = album;
                    image.Title = imageTitle.ToLower();
                    image.DateCreated = DateTime.UtcNow;
                    image.PublicationDate = DateTime.UtcNow;
                    image.LastModified = DateTime.UtcNow;
                    image.UrlName = Regex.Replace(imageTitle.ToLower(), @"[^\w\-\!\$\'\(\)\=\@\d_]+", "-");
                    image.AlternativeText = altText;

                    SFLibrariesManager.Upload(image, remoteImageStream, imageExtension);

                    SFLibrariesManager.RecompileItemUrls<Telerik.Sitefinity.Libraries.Model.Image>(image);

                    SFLibrariesManager.SaveChanges();

                    var bag = new Dictionary<string, string>();
                    bag.Add("ContentType", typeof(Telerik.Sitefinity.Libraries.Model.Image).FullName);
                    WorkflowManager.MessageWorkflow(masterImageId, typeof(Telerik.Sitefinity.Libraries.Model.Image), null, "Publish", false, bag);
                }
                else
                {
                    CreateAlbum(albumTitle);
                    CreateImage(albumTitle, imageTitle, remoteImageStream, imageExtension, altText);
                }
            }

            return true;
        }
        #endregion

        #region Content Stuff
        private string ParseContent(string content)
        {
            string parsedContent;

            parsedContent = WPAutoP(content);

            return parsedContent;
        }

        private string ImportAndLinkContentImages(string content, string postTitle, string blogTitle)
        {
            ImportContentImages(content, postTitle, blogTitle);

            content = Helpers.LinkContentImages(content);

            return content;
        }
        #endregion

        #region Remote Image Stuff
        private void ImportContentImages(string content, string postTitle, string blogTitle)
        {
            CQ document = content;

            if (!String.IsNullOrWhiteSpace(document.Render()))
            {
                var images = document.Select("img");

                images.Each((element) =>
                {
                    CQ cqElement = new CQ(element);

                    string imageSrc = cqElement.Attr("src");
                    string altText = cqElement.Attr("alt");
                    string imgFile = Path.GetFileName(imageSrc);

                    ImportImage(imageSrc, postTitle, blogTitle);
                });
            }
        }

        private bool ImportImage(string imageSrc, string postTitle, string blogTitle)
        {
            string imgFile = Path.GetFileName(imageSrc);
            string altText = "";

            if (imageSrc.StartsWith(".."))
            {
                imageSrc = CurrentDomain.Text + imageSrc.TrimStart('.');
            }

            bool result = false;
            Uri validUri;

            if (Uri.TryCreate(imageSrc, UriKind.Absolute, out validUri) && !Helpers.IsLocalPath(validUri.ToString()))
            {
                var sfImg = App.WorkWith().Images().Where(i => i.Status == ContentLifecycleStatus.Master).Get().ToList().Where(i => Path.GetFileName(i.FilePath) == imgFile.ToLower()).FirstOrDefault();

                if (sfImg == null)
                {
                    try
                    {
                        Stream remoteImageStream = DownloadRemoteImageFile(validUri.ToString());

                        if (remoteImageStream != null)
                        {
                            CreateImage("Images for " + blogTitle, Path.GetFileNameWithoutExtension(imgFile), remoteImageStream, Path.GetExtension(imgFile), altText);
                            result = true;
                        }
                        else
                        {
                            _importLog += "<br>Unable to retrieve image stream for '" + postTitle + "'<br>";
                            _importLog += "Image Src: " + imageSrc + "<br>";
                        }
                    }
                    catch (Exception ex)
                    {
                        _importLog += ex.Message + "|" + validUri;
                    }
                }
            }
            else
            {
                _importLog += "<br>Invalid image URI in post '" + postTitle + "'<br>";
                _importLog += "Image Src: " + imageSrc + "<br>";
            }

            return result;
        }
        #endregion

        #region Misc.
        private Stream DownloadRemoteImageFile(string uri)
        {
            Stream outputStream = new MemoryStream();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            try
            {
                using(HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode statusCode = response.StatusCode;

                    if ((statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Moved || statusCode == HttpStatusCode.Redirect) && response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                    {
                        using(Stream responseStream = response.GetResponseStream())
                        {
                            responseStream.CopyTo(outputStream);
                            return outputStream;
                        }
                    }
                    else
                    {
                        outputStream.Dispose();
                        return null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        #endregion

        #region Ported WP Functions
        private string WPAutoP(string pee, bool br = true)
        {
            Dictionary<string, string> pre_tags = null;

            if (pee.Trim() == "")
            {
                return "";
            }

            pee = pee + "\n"; // just to make things a little easier, pad the end

            if (pee.Contains("<pre") != false)
            {
                Stack<string> pee_parts = new Stack<string>(pee.Split(new string[] { "</pre" }, StringSplitOptions.None));

                string last_pee = pee_parts.Pop();
                pee = "";
                int i = 0;

                foreach (string pee_part in pee_parts)
                {
                    int start = pee_part.IndexOf("<pre");

                    // Malformed html?
                    if (start != -1)
                    {
                        pee += pee_part;
                        continue;
                    }

                    string name = "<pre wp-pre-tag-" + i + "></pre>";

                    pre_tags[name] = pee_part.Substring(start, pee_part.Length) + "</pre>";

                    pee += pee_part.Substring(0, start);
                    i++;
                }

                pee += last_pee;
            }

            pee = pee.PregReplace("<br />\\s*<br />", "\n\n");

            // Space things out a little
            string allblocks = "(?:table|thead|tfoot|caption|col|colgroup|tbody|tr|td|th|div|dl|dd|dt|ul|ol|li|pre|select|option|form|map|area|blockquote|address|math|style|p|h[1-6]|hr|fieldset|noscript|samp|legend|section|article|aside|hgroup|header|footer|nav|figure|figcaption|details|menu|summary)";

            pee = pee.PregReplace("(<" + allblocks + "[^>]*>)", "\n$1");
            pee = pee.PregReplace("(</" + allblocks + ">)", "$1\n\n");

            pee = pee.PregReplace("\r\n?", "\n"); // cross-platform newlines

            if (pee.IndexOf("<object") != -1)
            {
                pee = pee.PregReplace("\\s*<param([^>]*)>\\s*", @"<param$1>"); // no pee inside object/embed
                pee = pee.PregReplace("\\s*</embed>\\s*", @"</embed>");
            }

            pee = pee.PregReplace("\n\n+", "\n\n"); // take care of duplicates

            // make paragraphs, including one at the end
            string[] pees = Regex.Split(pee, "\n\\s*\n");

            pee = "";

            foreach (string tinkle in pees)
            {
                string tnkl = tinkle;

                pee += "<p>" + tnkl.TrimEnd('\r', '\n') + "</p>\n";
            }

            pee = pee.PregReplace("<p>\\s*</p>", ""); // under certain strange conditions it could create a P of entirely whitespace
            pee = pee.PregReplace("<p>([^<]+)</(div|address|form)>", "<p>$1</p></$2>");
            pee = pee.PregReplace("<p>\\s*(</?" + allblocks + "[^>]*>)\\s*</p>", "$1"); // don't pee all over a tag
            pee = pee.PregReplace("<p>(<li.+?)</p>", "$1"); // problem with nested lists

            pee = pee.PregReplace("<p><blockquote([^>]*)>", "<blockquote$1><p>");
            pee = pee.Replace("</blockquote></p>", "</p></blockquote>");

            pee = pee.PregReplace("<p>\\s*(</?" + allblocks + "[^>]*>)", "$1");
            pee = pee.PregReplace("(</?" + allblocks + "[^>]*>)\\s*</p>", "$1");

            // this still needs ported to C#
            //if ( br )
            //{
            //    pee = preg_replace_callback("/<(script|style).*?<\/\\1>/s", '_autop_newline_preservation_helper', pee);
            //    pee = pee.PregReplace(@"(?<!<br />)\s*\n", @"<br />\n"); // optionally make line breaks
            //    pee = pee.Replace(@"<WPPreserveNewline />", @"\n");
            //}

            pee = pee.PregReplace("(</?" + allblocks + "[^>]*>)\\s*<br />", "$1");
            pee = pee.PregReplace("<br />(\\s*</?(?:p|li|div|dl|dd|dt|th|pre|td|ul|ol)[^>]*>)", "$1");
            pee = pee.PregReplace("\n</p>$", "</p>");

            if (pre_tags != null && pre_tags.Count != 0)
            {
                foreach (KeyValuePair<string, string> p_t in pre_tags)
                {
                    pee = pee.Replace(p_t.Key, p_t.Value);
                }
            }

            return pee;
        }

        //function _autop_newline_preservation_helper( $matches ) {
        //    return str_replace("\n", "<WPPreserveNewline />", $matches[0]);
        //}
        #endregion

        #region Managers
        private static TaxonomyManager _sfTaxonomyManager;
        public static TaxonomyManager SFTaxonomyManager
        {
            get
            {
                if (_sfTaxonomyManager == null)
                {
                    _sfTaxonomyManager = TaxonomyManager.GetManager();
                }
                return _sfTaxonomyManager;
            }
        }

        private static LibrariesManager _sfLibrariesManager;
        public static LibrariesManager SFLibrariesManager
        {
            get
            {
                if (_sfLibrariesManager == null)
                {
                    _sfLibrariesManager = LibrariesManager.GetManager();
                }
                return _sfLibrariesManager;
            }
        }

        private static BlogsManager _sfBlogsManager;
        public static BlogsManager SFBlogsManager
        {
            get
            {
                if (_sfBlogsManager == null)
                {
                    _sfBlogsManager = BlogsManager.GetManager();
                }
                return _sfBlogsManager;
            }
        }

        private static UserManager _sfUserManager;
        public static UserManager SFUserManager
        {
            get
            {
                if (_sfUserManager == null)
                {
                    _sfUserManager = UserManager.GetManager();
                }
                return _sfUserManager;
            }
        }
        #endregion
    }

    #region Helpers
    internal static class Helpers
    {
        public static String PregReplace(this String input, string pattern, string replacement)
        {
            input = Regex.Replace(input, pattern, replacement);

            return input;
        }

        public static string LinkContentImages(string content)
        {
            CQ document = content;

            if (!String.IsNullOrWhiteSpace(document.Render()))
            {
                var images = document.Select("img");

                images.Each((element) =>
                {             
                    CQ cqElement = new CQ(element);

                    cqElement.BuildSitefinityReference();
                });
            }

            return document.Render();
        }

        public static bool BuildSitefinityReference(this CQ htmlElement)
        {
            bool result = false;

            string imgFile = Path.GetFileName(htmlElement.Attr("src"));

            var sfImg = App.WorkWith().Images().Where(i => i.Status == ContentLifecycleStatus.Master).Get().ToList().Where(i => Path.GetFileName(i.FilePath) == imgFile.ToLower()).FirstOrDefault();

            if (sfImg != null)
            {
                var manager = LibrariesManager.GetManager();
                var album = manager.GetAlbums().Where(a => a.Id == sfImg.Album.Id).FirstOrDefault();

                string urlRoot = Telerik.Sitefinity.Configuration.Config.Get<LibrariesConfig>().Images.UrlRoot;

                var sfImgLive = App.WorkWith().Image(sfImg.Id).GetLive().Get();

                var sfRef = "[" + urlRoot + "|" + (LibrariesDataProvider)album.Provider + "]" + sfImgLive.Id;

                htmlElement.Attr("sfref", sfRef);

                string relativeUrl = new Uri(sfImg.MediaUrl).AbsolutePath;

                htmlElement.Attr("src", relativeUrl);

                result = true;
            }

            return result;
        }

        public static bool IsLocalPath(string p)
        {
            if (p.StartsWith("http:\\"))
            {
                return false;
            }

            return new Uri(p).IsFile;
        }
    }
    #endregion

    public class GenericBlogPost
    {
        public string Creator;
        public string Title;
        public string PostName;
        public string AttachmentUrl;
        public string Summary;
        public string Content;
        public DateTime DateCreated;
        public DateTime PublicationDate;
        public DateTime LastModified;
        public string Link;
        public bool AllowComments;
        public List<string> Tags;
        public List<string> Categories;
        public Dictionary<string, string> Meta;
        public GenericPostComment[] Comments;
    }

    public class GenericPostComment
    {
        public string Author;
        public string AuthorEmail;
        public string AuthorIP;
        public string AuthorURL;
        public DateTime CommentDate;
        public DateTime CommentDateGMT;
        public string Content;
        public string Status;
        public string Type;
        public int Parent;
        public int UserId;
    }

    public class PostAuthor
    {
        public int Id;
        public string Username;
        public string Email;
        public string Nickname;
        public string FirstName;
        public string LastName;
    }
}
