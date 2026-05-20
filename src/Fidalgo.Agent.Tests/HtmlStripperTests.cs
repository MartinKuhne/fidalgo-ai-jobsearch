using Fidalgo.Agent.Sanitization;

namespace Fidalgo.Agent.Tests;

public class HtmlStripperTests
{
    private readonly HtmlStripper _stripper;

    public HtmlStripperTests()
    {
        _stripper = new HtmlStripper();
    }

    [Fact]
    public void Strip_ShouldReturnEmptyString_WhenInputIsNullOrEmpty()
    {
        Assert.Equal(string.Empty, _stripper.Strip(null!));
        Assert.Equal(string.Empty, _stripper.Strip(string.Empty));
    }

    [Fact]
    public void Strip_ShouldRemoveScriptTags()
    {
        var html = "<html><body><script>alert('hello');</script><p>Hello World</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void Strip_ShouldRemoveStyleTags()
    {
        var html = "<html><body><style>body{color:red;}</style><p>Hello</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("color:red", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void Strip_ShouldRemoveImageTags()
    {
        var html = "<html><body><img src='photo.jpg' alt='A photo'/><p>Content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("img", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("photo.jpg", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveNavElements()
    {
        var html = "<html><body><nav><a href='/home'>Home</a></nav><main><p>Main content</p></main></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("nav", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Home", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Main content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveFooterElements()
    {
        var html = "<html><body><footer><p>Copyright 2024</p></footer><article><p>Article text</p></article></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("footer", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Copyright", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Article text", result);
    }

    [Fact]
    public void Strip_ShouldRemoveAsideElements()
    {
        var html = "<html><body><main><p>Main article</p></main><aside><p>Sidebar ads</p></aside></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("aside", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sidebar", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Main article", result);
    }

    [Fact]
    public void Strip_ShouldRemoveAdElementsByClass()
    {
        var html = "<html><body><div class='ad-container'><p>Advertisement</p></div><p>Real content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("Advertisement", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Real content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveSponsoredElementsByClass()
    {
        var html = "<html><body><div class='sponsored-link'><p>Sponsored job</p></div><p>Organic job</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("Sponsored", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Organic job", result);
    }

    [Fact]
    public void Strip_ShouldRemoveElementsByAdId()
    {
        var html = "<html><body><div id='ad-banner'><p>Banner ad</p></div><p>Content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("Banner ad", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveFormElements()
    {
        var html = "<html><body><form><input type='text'/><button>Submit</button></form><p>Content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("input", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("button", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveHeadSection()
    {
        var html = "<html><head><title>Test</title><meta name='description' content='Test'/></head><body><p>Body content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("head", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test", result);
        Assert.Contains("Body content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveInlineStyles()
    {
        var html = "<html><body><p style='color: red;'>Red text</p><span style='font-weight: bold;'>Bold</span></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("style=", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("color:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Red text", result);
        Assert.Contains("Bold", result);
    }

    [Fact]
    public void Strip_ShouldRemoveEventAttributes()
    {
        var html = "<html><body><a href='/link' onclick='trackClick()'>Link</a><div onmouseover='showTooltip()'>Hover text</div></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Link", result);
        Assert.Contains("Hover text", result);
    }

    [Fact]
    public void Strip_ShouldRemoveCookieBanner()
    {
        var html = "<html><body><div class='cookie-consent'>Accept cookies</div><p>Main content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("cookie", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Accept cookies", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Main content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveSocialShareButtons()
    {
        var html = "<html><body><div class='social-share'><button>Share on Twitter</button></div><p>Article</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("social", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Share on Twitter", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Article", result);
    }

    [Fact]
    public void Strip_ShouldPreserveSemanticHtmlTags()
    {
        var html = "<html><body><article><h1>Job Title</h1><p>Job description with <strong>bold</strong> and <em>italic</em> text</p></article></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("h1", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Job Title", result);
        Assert.Contains("strong", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bold", result);
        Assert.Contains("em", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("italic", result);
    }

    [Fact]
    public void Strip_ShouldPreserveLists()
    {
        var html = "<html><body><ul><li>Requirement 1</li><li>Requirement 2</li></ul><ol><li>Step one</li></ol></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("Requirement 1", result);
        Assert.Contains("Requirement 2", result);
        Assert.Contains("Step one", result);
    }

    [Fact]
    public void Strip_ShouldPreserveLinks()
    {
        var html = "<html><body><p>Read more at <a href='https://example.com'>Example</a></p></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("href=", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example.com", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Example", result);
    }

    [Fact]
    public void Strip_ShouldRemoveVideoAndAudioTags()
    {
        var html = "<html><body><video src='intro.mp4'></video><audio src='podcast.mp3'></audio><p>Text content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("video", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audio", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("intro.mp4", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Text content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveIframeTags()
    {
        var html = "<html><body><iframe src='https://ads.example.com/ad'></iframe><p>Real content</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ads.example.com", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Real content", result);
    }

    [Fact]
    public void Strip_ShouldRemoveEmptyElementsAfterStripping()
    {
        var html = "<html><body><div><script>var x = 1;</script></div><p>Keep this</p></body></html>";
        var result = _stripper.Strip(html);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep this", result);
    }

    [Fact]
    public void Strip_ShouldPreserveTableStructure()
    {
        var html = "<html><body><table><tr><th>Name</th><th>Salary</th></tr><tr><td>John</td><td>$100k</td></tr></table></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("table", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Name", result);
        Assert.Contains("Salary", result);
        Assert.Contains("John", result);
        Assert.Contains("$100k", result);
    }

    [Fact]
    public void Strip_ShouldPreserveBlockquote()
    {
        var html = "<html><body><blockquote><p>Quoted text</p></blockquote></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("blockquote", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quoted text", result);
    }

    [Fact]
    public void Strip_ShouldPreserveCodeBlocks()
    {
        var html = "<html><body><p>Use <code>console.log()</code> for debugging</p><pre><code>function hello() { return 'world'; }</code></pre></body></html>";
        var result = _stripper.Strip(html);

        Assert.Contains("code", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("console.log()", result);
        Assert.Contains("pre", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Strip_ShouldHandleComplexIndeedJobPageStructure()
    {
        var html = @"
<html>
<body>
    <header>Site Header</header>
    <nav class='main-nav'>
        <a href='/jobs'>Jobs</a>
        <a href='/companies'>Companies</a>
    </nav>
    <div id='ad-sidebar'>
        <img src='ad-banner.jpg'/>
        <p>Sponsored</p>
    </div>
    <div class='cookie-banner'>
        <p>We use cookies</p>
    </div>
    <main>
        <article class='job-posting'>
            <h1>Software Engineer</h1>
            <p>We are looking for a skilled engineer...</p>
            <ul>
                <li>C# experience required</li>
                <li>.NET Core preferred</li>
            </ul>
            <div class='salary'>$120k - $150k</div>
        </article>
    </main>
    <aside class='related-jobs'>
        <h3>Similar Jobs</h3>
        <p>Backend Developer</p>
    </aside>
    <footer>
        <p>Copyright 2024 Indeed</p>
    </footer>
    <script src='/app.js'></script>
    <script>analytics.track();</script>
    <link rel='stylesheet' href='/styles.css'/>
</body>
</html>";

        var result = _stripper.Strip(html);

        Assert.DoesNotContain("Site Header", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("main-nav", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ad-sidebar", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ad-banner.jpg", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie-banner", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("We use cookies", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("related-jobs", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Similar Jobs", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Copyright", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("app.js", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("analytics.track", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("styles.css", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("header>", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("footer>", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nav", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aside", result, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Software Engineer", result);
        Assert.Contains("skilled engineer", result);
        Assert.Contains("C# experience required", result);
        Assert.Contains(".NET Core preferred", result);
        Assert.Contains("$120k", result);
    }
}
