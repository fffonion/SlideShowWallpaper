using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class DisplayPowerPausePolicyTests
{
    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    [Fact]
    public void GetPauseState_WithSuspendEvent_ReturnsTrue()
    {
        bool? result = DisplayPowerPausePolicy.GetPauseState(4);

        Assert.True(result);
    }

    [Fact]
    public void GetPauseState_WithResumeEvent_ReturnsFalse()
    {
        bool? result = DisplayPowerPausePolicy.GetPauseState(18);

        Assert.False(result);
    }

    [Fact]
    public void IsResumeEvent_WithResumeEvents_ReturnsTrue()
    {
        Assert.True(DisplayPowerPausePolicy.IsResumeEvent(18));
        Assert.True(DisplayPowerPausePolicy.IsResumeEvent(7));
    }

    [Fact]
    public void IsResumeEvent_WithDisplayPowerEvent_ReturnsFalse()
    {
        Assert.False(DisplayPowerPausePolicy.IsResumeEvent(32787));
    }

    [Fact]
    public void GetPauseState_WithDisplayOff_ReturnsTrue()
    {
        bool? result = DisplayPowerPausePolicy.GetPauseState(32787, ConsoleDisplayState, 0);

        Assert.True(result);
    }

    [Fact]
    public void GetPauseState_WithDisplayOn_ReturnsFalse()
    {
        bool? result = DisplayPowerPausePolicy.GetPauseState(32787, ConsoleDisplayState, 1);

        Assert.False(result);
    }

    [Fact]
    public void GetPauseState_WithUnrelatedPowerSetting_ReturnsNull()
    {
        bool? result = DisplayPowerPausePolicy.GetPauseState(32787, Guid.NewGuid(), 0);

        Assert.Null(result);
    }
}
