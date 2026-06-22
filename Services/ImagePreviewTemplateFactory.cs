using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace SlideShowWallpaper.Services;

public static class ImagePreviewTemplateFactory
{
    public const string TemplateText =
        """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Padding="6" RowSpacing="5">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>
                <Border Height="116" CornerRadius="4" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}">
                    <Image Source="{Binding Thumbnail}" Stretch="Uniform" />
                </Border>
                <TextBlock Grid.Row="1" Text="{Binding FileName}" TextTrimming="CharacterEllipsis" FontSize="12" />
                <TextBlock Grid.Row="2" Text="{Binding Details}" TextTrimming="CharacterEllipsis" FontSize="11" Opacity="0.72" />
            </Grid>
        </DataTemplate>
        """;

    public static DataTemplate Create()
    {
        return (DataTemplate)XamlReader.Load(TemplateText);
    }
}
