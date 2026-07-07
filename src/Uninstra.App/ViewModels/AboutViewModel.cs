namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed partial class AboutViewModel : ObservableObject
{
    public string AppName => Core.UninstraInfo.AppName;
    public string Tagline => Core.UninstraInfo.Tagline;
    public string Version => Core.UninstraInfo.Version;
    public string Publisher => Core.UninstraInfo.Publisher;
    public string Privacy => "Uninstra runs locally. Uninstra does not send application lists, files, browser extensions, or user activity to any server.";
    public string License => "MIT License";
    public string Disclaimer => "Users should review leftovers before cleanup. No uninstaller can guarantee all leftovers are safe to remove. Runtimes and system components should be handled with care. Backup is recommended.";
}
