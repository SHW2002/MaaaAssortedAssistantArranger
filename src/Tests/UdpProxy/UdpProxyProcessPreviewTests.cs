namespace Game_Daily_Routine_Launcher.Tests;

public class UdpProxyProcessPreviewTests
{
    [Fact]
    public void PresetPreviewAndValidationUseTheTaskIdentityAndKeepAutomaticModeEmpty()
    {
        var config = TaskFakes.Config();
        config.Id = "march7th";
        config.GameProcessName = "March7thAssistant";
        config.UdpProxy.ProcessNames = string.Empty;
        config.ToolPath = "test://start";
        config.LaunchMode = LaunchMode.Uri;
        var vm = new GameTaskViewModel(config);
        Assert.False(vm.HasValidationError, vm.ValidationMessage);
        Assert.Equal("自动预设 · 星穹铁道：StarRail.exe", vm.UdpProxyProcessPreview);
        Assert.Empty(vm.ToConfig().UdpProxy.ProcessNames);
        Assert.Equal(new[] { "StarRail.exe" }, UdpProxyRules.Validate(vm.ToConfig()));
    }

    [Fact]
    public void EditingTheMonitorRefreshesPreviewWithoutDuplicateSavesOrStaleTargets()
    {
        var config = TaskFakes.Config();
        config.Name = "Custom task";
        config.GameProcessName = "First.Game";
        config.UdpProxy.ProcessNames = string.Empty;
        var vm = new GameTaskViewModel(config);
        var saves = 0;
        var previews = 0;
        vm.ConfigChanged = () => saves++;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.UdpProxyProcessPreview)) previews++; };
        vm.GameProcessName = "Second.Game.exe";
        Assert.Equal(1, saves);
        Assert.Equal(1, previews);
        Assert.Equal("自动跟随监控进程：Second.Game.exe", vm.UdpProxyProcessPreview);
        Assert.Equal(new[] { "Second.Game.exe" }, UdpProxyRules.Validate(vm.ToConfig()));

        vm.UdpProxyProcessNames = "Override.exe";
        Assert.Equal(2, saves);
        Assert.Equal("自定义覆盖：Override.exe", vm.UdpProxyProcessPreview);
        vm.GameProcessName = "Third";
        Assert.Equal("自定义覆盖：Override.exe", vm.UdpProxyProcessPreview);
        vm.UdpProxyProcessNames = string.Empty;
        Assert.Equal(4, saves);
        Assert.Equal("自动跟随监控进程：Third.exe", vm.UdpProxyProcessPreview);
        Assert.Empty(vm.ToConfig().UdpProxy.ProcessNames);
    }

    [Fact]
    public void RenamingAnImportedTaskUpdatesItsAutomaticPreset()
    {
        var config = TaskFakes.Config();
        config.Name = "Custom task";
        config.GameProcessName = "Assistant";
        config.UdpProxy.ProcessNames = string.Empty;
        var vm = new GameTaskViewModel(config);
        var saves = 0;
        vm.ConfigChanged = () => saves++;
        vm.Name = "星穹铁道 (March7th)";
        Assert.Equal(1, saves);
        Assert.Equal("自动预设 · 星穹铁道：StarRail.exe", vm.UdpProxyProcessPreview);
        Assert.Equal(new[] { "StarRail.exe" }, UdpProxyRules.Validate(vm.ToConfig()));
    }
}
