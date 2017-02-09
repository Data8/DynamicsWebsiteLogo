using System;
using System.Activities;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Xml;
using Data8.Crm.WebsiteLogo.Css;
using HtmlAgilityPack;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;

namespace Data8.Crm.WebsiteLogo
{
    public class WebsiteLogoActivity : CodeActivity
    {
        [Input("Website")]
        [RequiredArgument]
        public InArgument<string> Website { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            var workflowContext = executionContext.GetExtension<IWorkflowContext>();
            var orgFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            var logger = executionContext.GetExtension<ITracingService>();
            var org = orgFactory.CreateOrganizationService(workflowContext.UserId);
            
            // Get the requested website from the workflow
            var website = Website.Get(executionContext);

            // Nothing to do if no website was supplied
            if (String.IsNullOrEmpty(website))
            {
                logger.Trace("No website was supplied");
                return;
            }

            logger.Trace("Website was supplied: [{0}]", website);

            try
            {
                // Scrape the website to get the logo
                var image = GetLogo(logger, website);

                if (image == null)
                {
                    logger.Trace("Couldn't find any image");
                    throw new InvalidPluginExecutionException("Couldn't find any image");
                }

                // Update the entity the workflow is being run on with the image
                var updatedEntity = new Entity(workflowContext.PrimaryEntityName)
                {
                    Id = workflowContext.PrimaryEntityId,
                    ["entityimage"] = image
                };

                org.Update(updatedEntity);
            }
            catch (Exception ex)
            {
                // Don't care about errors getting the website logo.
                logger.Trace("Exception thrown: {0}", ex);

                throw new InvalidPluginExecutionException(ex.Message);
            }
        }

        /// <summary>
        /// Scrape a website to get a logo.
        /// </summary>
        /// <param name="website">The URL of the website to scrape.</param>
        /// <returns>An image in the correct format for use in Microsoft Dynamics CRM, or null if no image could be found</returns>
        public static byte[] GetLogo(ITracingService logger, string website)
        {
            // Standardise the website. It may or may not have http(s):// at the start.
            if (website.IndexOf("://", StringComparison.Ordinal) == -1)
            {
                if (!website.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    website = "http://www." + website;
                else
                    website = "http://" + website;
            }

            // Retrieve the home page of the website
            var request = WebRequest.Create(website);
            using (var response = request.GetResponse())
            using (var content = response.GetResponseStream())
            {
                if (content == null)
                    return null;

                var websiteUri = ((HttpWebResponse)response).ResponseUri;

                logger.Trace("Got response from [{0}]", websiteUri);

                // Parse the HTML
                var html = new HtmlDocument();
                html.Load(content);

                logger.Trace("Parsed HTML");

                if (html.DocumentNode == null)
                    throw new InvalidPluginExecutionException("Could not identify DocumentNode in HTML");

                // Identify any stylesheets included in the page, download and parse them
                var css = new Dictionary<Stylesheet, Uri>();

                var links = html.DocumentNode.SelectNodes("//link[@rel='stylesheet']");

                if (links != null)
                {
                    foreach (var link in links)
                    {
                        var cssUrl = new Uri(websiteUri, link.GetAttributeValue("href", ""));

                        logger.Trace("Loading CSS from [{0}]", cssUrl);

                        var cssRequest = WebRequest.Create(cssUrl);
                        using (var cssResponse = cssRequest.GetResponse())
                        using (var cssContent = cssResponse.GetResponseStream())
                        {
                            if (cssContent == null)
                                continue;

                            using (var cssReader = new StreamReader(cssContent))
                            {
                                var cssText = cssReader.ReadToEnd();
                                var stylesheet = new Stylesheet(cssText);
                                css.Add(stylesheet, ((HttpWebResponse) cssResponse).ResponseUri);
                            }
                        }
                    }
                }

                logger.Trace("Parsed CSS");

                // Convert the values of the src attributes of img elements to lower case to simplify
                // searching later on
                var images = html.DocumentNode.SelectNodes("//img");

                if (images != null)
                {
                    foreach (var i in images)
                        i.SetAttributeValue("lowersrc", i.GetAttributeValue("src", "").ToLower());
                }

                // Find the first node that matches a set of queries that typically identify logo images
                var xpaths = new[] {
                            "//*[contains(@id, 'logo')]",
                            "//*[contains(@class, 'logo')]",
                            "//img[contains(@lowersrc, 'logo')]",
                            "//link[@rel = 'icon']",
                            "//link[@rel = 'shortcut icon']",
                            "//body"
                        };

                Uri backgroundUrl = null;
                var backgroundColor = Color.Transparent;
                Uri imgUrl = null;

                foreach (var xpath in xpaths)
                {
                    var container = html.DocumentNode.SelectSingleNode(xpath);

                    if (container == null)
                        continue;

                    var img = container;

                    // If the node we found wasn't an img element, see if we can find one inside this node
                    if (container.Name.ToLower() != "img")
                        img = container.Descendants("img").FirstOrDefault();

                    if (img != null)
                    {
                        // If we've found an img element we can just take the src attribute as the image URL
                        imgUrl = new Uri(websiteUri, img.GetAttributeValue("src", ""));
                    }
                    else if (container.Name.ToLower() == "link" &&
                             (container.GetAttributeValue("rel", "") == "icon" ||
                              container.GetAttributeValue("rel", "") == "shortcut icon"))
                    {
                        // If we've found a link element we can just take the href attribute as the image URL
                        imgUrl = new Uri(websiteUri, container.GetAttributeValue("href", ""));
                    }
                    else if (xpath != "//body")
                    {
                        // See if we've got a background image to use instead.
                        imgUrl = GetNestedBackgroundUrl(css, container);
                    }

                    if (img != null)
                    {
                        // See if any parent nodes have a background image to put behind the logo.
                        var parent = img.ParentNode;
                        while (parent != null && backgroundUrl == null && backgroundColor == Color.Transparent)
                        {
                            backgroundUrl = GetBackgroundUrl(css, parent);
                            backgroundColor = GetBackgroundColor(css, parent);
                            parent = parent.ParentNode;
                        }
                    }

                    if (imgUrl != null)
                        break;
                }

                // Fallback to favicon.ico in the website root
                if (imgUrl == null)
                    imgUrl = new Uri(websiteUri, "/favicon.ico");

                logger.Trace("Loading image from URL [{0}]", imgUrl);

                // Now we've got the URL of an image, we need to retrieve it and convert it into a format
                // suitable for CRM. CRM expects a 144x144 PNG image
                var image = LoadImage(imgUrl);

                if (image == null)
                    return null;

                if (backgroundColor == Color.Transparent)
                    backgroundColor = Color.White;

                var resizedImage = new Bitmap(144, 144);
                using (var g = Graphics.FromImage(resizedImage))
                {
                    // Add the background color.
                    using (var brush = new SolidBrush(backgroundColor))
                    {
                        g.FillRectangle(brush, new Rectangle(Point.Empty, resizedImage.Size));
                    }

                    // Add the background image.
                    if (backgroundUrl != null)
                    {
                        try
                        {
                            var bgImage = LoadImage(backgroundUrl);

                            if (bgImage != null)
                                g.DrawImage(bgImage, Point.Empty);
                        }
                        catch
                        {
                            // Don't worry about missing background images too much.
                        }
                    }

                    // Add the logo.
                    var ratio = (double)image.Height / image.Width;
                    var targetHeight = resizedImage.Height;
                    var targetWidth = resizedImage.Width;

                    if (ratio < 1)
                        targetHeight = (int)(targetHeight * ratio);
                    else
                        targetWidth = (int)(targetWidth / ratio);

                    g.DrawImage(image, new Rectangle((resizedImage.Width - targetWidth) / 2, (resizedImage.Height - targetHeight) / 2, targetWidth, targetHeight));
                }

                using (var pngStream = new MemoryStream())
                {
                    resizedImage.Save(pngStream, ImageFormat.Png);
                    pngStream.Close();

                    return pngStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the URL of a background image for a node
        /// </summary>
        /// <param name="css">The list of stylesheets that are applied to the page</param>
        /// <param name="node">The node to get the background image for</param>
        /// <returns>The fully qualified URL of the background image that applies to the requested node or its children</returns>
        /// <remarks>
        /// If no background image is specified in the <paramref name="css"/> stylesheets, this method will
        /// recursively search through the node's descendents and return the URL of the first background image it finds.
        /// If no background image is found in any descendent it will return null.
        /// </remarks>
        private static Uri GetNestedBackgroundUrl(Dictionary<Stylesheet, Uri> css, HtmlNode node)
        {
            var uri = GetBackgroundUrl(css, node);

            if (uri != null)
                return uri;

            foreach (var child in node.ChildNodes)
            {
                uri = GetNestedBackgroundUrl(css, child);

                if (uri != null)
                    return uri;
            }

            return null;
        }

        /// <summary>
        /// Gets the background color for a node
        /// </summary>
        /// <param name="css">The list of stylesheets that are applied to the page</param>
        /// <param name="node">The node to get the background color for</param>
        /// <returns>The background color applied to the node, or <see cref="Color.Transparent"/> if no background color is defined</returns>
        private static Color GetBackgroundColor(Dictionary<Stylesheet, Uri> css, HtmlNode node)
        {
            foreach (var stylesheet in css)
            {
                var style = stylesheet.Key.GetAppliedStyles(node);
                var color = GetBackgroundColor(style);

                if (color != Color.Transparent)
                    return color;
            }

            return Color.Transparent;
        }

        /// <summary>
        /// Gets the background color from a set of CSS style properties
        /// </summary>
        /// <param name="style">The style properties applied to a node</param>
        /// <returns>The background color defined by the styles, or <see cref="Color.Transparent"/> if no background color is defined</returns>
        private static Color GetBackgroundColor(IDictionary<string, string> style)
        {
            string value;
            if (style.TryGetValue("background-color", out value))
                return ColorTranslator.FromHtml(value);

            return Color.Transparent;
        }

        /// <summary>
        /// Gets the URL of a background image for a node
        /// </summary>
        /// <param name="css">The list of stylesheets that are applied to the page</param>
        /// <param name="node">The node to get the background image for</param>
        /// <returns>The fully qualified URL of the background image that applies to the requested node</returns>
        private static Uri GetBackgroundUrl(Dictionary<Stylesheet, Uri> css, HtmlNode node)
        {
            foreach (var stylesheet in css)
            {
                var style = stylesheet.Key.GetAppliedStyles(node);
                var imageUrl = GetBackgroundUrl(style);

                if (imageUrl != null)
                    return new Uri(stylesheet.Value, imageUrl);
            }

            return null;
        }

        /// <summary>
        /// Gets the URL of a background image for a node
        /// </summary>
        /// <param name="style">The style properties applied to a node</param>
        /// <returns>The URL of a background image applied to a node, or null if no background image is defined</returns>
        private static string GetBackgroundUrl(IDictionary<string, string> style)
        {
            string value;
            if (style.TryGetValue("background-image", out value))
                return StripUrl(value);

            return null;
        }

        /// <summary>
        /// Extracts a URL from a CSS definition
        /// </summary>
        /// <param name="value">The URL value, e.g. "url('http://.....')"</param>
        /// <returns>The URL contained in the <paramref name="value"/></returns>
        private static string StripUrl(string value)
        {
            value = value.Substring(4, value.Length - 5);
            if (value.StartsWith("'") || value.StartsWith("\""))
                value = value.Substring(1, value.Length - 2);

            return value;
        }

        /// <summary>
        /// Loads an image from a URL
        /// </summary>
        /// <param name="uri">The URL of the image to load</param>
        /// <returns>The loaded image</returns>
        private static Image LoadImage(Uri uri)
        {   
            var request = WebRequest.Create(uri);
            using (var response = request.GetResponse())
            using (var content = response.GetResponseStream())
            {
                if (content == null)
                    return null;

                if (response.ContentType == "image/svg+xml")
                {
                    var xml = new XmlDocument();
                    xml.Load(content);
                    var svg = Svg.SvgDocument.Open(xml);
                    return svg.Draw(144, 144);
                }

                return Image.FromStream(content);
            }
        }
    }
}