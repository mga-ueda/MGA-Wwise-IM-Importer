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

    [Fact]
    public void CloneForNewProject_CopiesSettings_AndClearsWavePaths()
    {
        var source = ProjectSettingsStore.CreateAppDefaults("SourceProject");
        source.OutputDirectory = @"D:\Exports";
        source.FadeInSeconds = 0.5d;
        source.FadeOutSeconds = 1.0d;
        source.GridOverride = MarkerGridOverrideMode.Beat;
        source.CommentPrefix = "BGM_";
        source.CommentPrefixEnabled = true;
        source.LastWavePath = @"C:\Sounds\track.wav";
        source.LastWavePaths = @"C:\Sounds\track1.wav|C:\Sounds\track2.wav";
        source.KeepTarget = true;
        source.KeptTargetPath = @"\Actor-Mixer Hierarchy\Music";

        var newProject = source.CloneForNewProject("NewProject");

        // プロジェクト名と設定は反映・コピーされる
        Assert.Equal("NewProject", newProject.Name);
        Assert.Equal(@"D:\Exports", newProject.OutputDirectory);
        Assert.Equal(0.5d, newProject.FadeInSeconds);
        Assert.Equal(1.0d, newProject.FadeOutSeconds);
        Assert.Equal(MarkerGridOverrideMode.Beat, newProject.GridOverride);
        Assert.Equal("BGM_", newProject.CommentPrefix);
        Assert.True(newProject.CommentPrefixEnabled);
        Assert.True(newProject.KeepTarget);
        Assert.Equal(@"\Actor-Mixer Hierarchy\Music", newProject.KeptTargetPath);

        // ドロップされた波形ファイル情報はクリアされる
        Assert.Empty(newProject.LastWavePath);
        Assert.Empty(newProject.LastWavePaths);
    }
}
