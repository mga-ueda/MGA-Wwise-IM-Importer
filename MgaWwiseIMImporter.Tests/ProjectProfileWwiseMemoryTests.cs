using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Tests;

public class ProjectProfileWwiseMemoryTests
{
    [Fact]
    public void ProfileData_RoundtripsLastKnownWwiseProject()
    {
        var profile = ProjectSettingsStore.CreateAppDefaults("Game A");
        profile.LastKnownWwiseProjectName = "MyGame";
        profile.LastKnownWwiseProjectFilePath = @"D:\Wwise\MyGame.wproj";

        var restored = ProjectProfileData.FromProfile(profile).ToProfile();

        Assert.Equal("MyGame", restored.LastKnownWwiseProjectName);
        Assert.Equal(@"D:\Wwise\MyGame.wproj", restored.LastKnownWwiseProjectFilePath);
    }

    [Fact]
    public void Clone_CopiesLastKnownWwiseProject()
    {
        var profile = ProjectSettingsStore.CreateAppDefaults();
        profile.LastKnownWwiseProjectName = "MyGame";
        profile.LastKnownWwiseProjectFilePath = @"D:\Wwise\MyGame.wproj";

        var clone = profile.Clone();

        Assert.Equal(profile.LastKnownWwiseProjectName, clone.LastKnownWwiseProjectName);
        Assert.Equal(profile.LastKnownWwiseProjectFilePath, clone.LastKnownWwiseProjectFilePath);
    }
}
