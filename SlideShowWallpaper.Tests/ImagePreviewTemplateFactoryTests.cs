using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ImagePreviewTemplateFactoryTests
{
    [Fact]
    public void TemplateText_UsesUniformPreviewStretch()
    {
        string template = ImagePreviewTemplateFactory.TemplateText;

        Assert.Contains("Stretch=\"Uniform\"", template);
        Assert.DoesNotContain("UniformToFill", template);
    }
}
