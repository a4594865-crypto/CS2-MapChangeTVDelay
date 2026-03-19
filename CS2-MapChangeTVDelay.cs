using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Cvars;

namespace CS2MapChangeStopTV;

public class MapChangeStopTV : BasePluginConfig
{
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
}

public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTV>
{
    public override string ModuleName => "CS2-MapChangeStopTV-InterceptOnly";
    public override string ModuleVersion => "0.0.7";
    public override string ModuleAuthor => "Letaryat & Gemini";
    
    public required MapChangeStopTV Config { get; set; }
    public void OnConfigParsed(MapChangeStopTV config) { Config = config; }

    public override void Load(bool hotReload)
    {
        LogDebug("CS2-MapChangeStopTV (攔截專用版) - Loaded");

        // 1. 攔截換圖指令 (防止手動換圖時當機)
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        // 2. 監聽比賽結束事件 (官方 UI 出現後 5 秒強行切斷)
        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            LogDebug("結算面板已顯示，將在 5 秒後強制攔截並關閉所有錄影。");

            AddTimer(5.0f, () => 
            {
                StopRecord();
            });

            return HookResult.Continue;
        });

        // 3. 監聽地圖結束 (最後一道防線)
        RegisterListener<OnMapEnd>(() =>
        {
            StopRecord();
        });
    }

    public override void Unload(bool hotReload)
    {
        StopRecord();
    }

    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        LogDebug("偵測到換圖指令，正在搶先關閉錄影...");
        StopRecord();
        return HookResult.Continue;
    }

    public void LogDebug(string message)
    {
        if (Config.Debug == false) { return; }
        Logger.LogInformation($"[StopTV-Intercept] {message}");
    }

    public void StopRecord()
    {
        // 暴力執行停止錄影，不管錄影是誰開啟的
        Server.ExecuteCommand("tv_stoprecord");
        LogDebug("已下達 tv_stoprecord 指令。");
    }
}
