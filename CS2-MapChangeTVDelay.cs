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
    [JsonPropertyName("ReplayName")] public string ReplayName { get; set; } = "demo-{mapname}-{servername}-{date}-{time}-{timedate}";
    [JsonPropertyName("DemoDirectory")] public string DemoDirectory { get; set; } = "replays";
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
}
public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTV>
{
    public override string ModuleName => "CS2-MapChangeStopTV";
    public override string ModuleVersion => "0.0.6";
    public override string ModuleAuthor => "Letaryat";
    public override string ModuleDescription => "Delays map change and stops tv record";
    public string? fileName;
    private bool isRecording = false;
    public required MapChangeStopTV Config { get; set; }
    public void OnConfigParsed(MapChangeStopTV config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        LogDebug("CS2-MapChangeStopTV - Loaded");

        try
        {
            if (!Directory.Exists(Path.Combine(Server.GameDirectory, "csgo", Config.DemoDirectory)))
            {
                Directory.CreateDirectory(Path.Combine(Server.GameDirectory, "csgo", Config.DemoDirectory));
                LogDebug($"Created a new directory: {Config.DemoDirectory}");
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Error while creating a dir: {ex}");
        }

        //Listeners:
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        RegisterListener<OnMapStart>((mapname) =>
        {
            fileName = null;
            LogDebug($"New map start ({mapname}). Filename = null");
        });

        RegisterListener<OnMapEnd>(() =>
        {
            if(fileName != null)
            {
                LogDebug($"Ending map, ending demo!");
                StopRecord();
            }
        });

        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            // 當結算面板 (Win Panel) 出現時，啟動 5 秒計時器
            // 這 5 秒剛好夠錄完 MVP 動畫與最終比分，然後在投票 UI 變得明顯前切斷
            LogDebug("Win Panel 顯示中，將在 5 秒後停止錄影並斷開 CSTV。");

            AddTimer(5.0f, () => 
            {
                if (isRecording)
                {
                    LogDebug("5 秒已到，正式執行 tv_stoprecord。");
                    StopRecord();
                }
            });

            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundStart>((e, i) =>
        {
            if (fileName != null) { return HookResult.Continue; }
            fileName = Config.ReplayName;
            var placeholdersReplacement = new Dictionary<string, string>
            {
                {"{mapname}", Server.MapName},
                {"{servername}", ConVar.Find("hostname")?.StringValue ?? "Unknown"},
                {"{date}", DateTime.Now.ToString("yyyy-MM-dd")},
                {"{time}", DateTime.Now.ToString("HH-mm-ss")},
                {"{timedate}", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")},
                {"{1s1kformat}", $"auto-{DateTime.Now:yyyyMMdd-HHmm}-{Server.MapName}"}
            };
            foreach (var placeholder in placeholdersReplacement)
            {
                fileName = fileName.Replace(placeholder.Key, placeholder.Value);
            }
            isRecording = true;
            Server.NextWorldUpdate(() => Server.ExecuteCommand($"tv_record \"{Config.DemoDirectory}/{fileName}.dem\""));
            LogDebug($"Recording demo: {fileName}");
            return HookResult.Continue;
        });
    }
    public override void Unload(bool hotReload)
    {
        LogDebug("CS2-MapChangeStopTV - Unloaded");
        StopRecord();
    }
    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        LogDebug("Stopped TV Record before changing map.");
        StopRecord();
        return HookResult.Continue;
    }
    public void LogDebug(string message)
    {
        if (Config.Debug == false) { return; }
        Logger.LogInformation($"{DateTime.Now} | {message}");
    }
    public void StopRecord()
    {
        if (isRecording)
        {
            Server.ExecuteCommand("tv_stoprecord");
            if(fileName != null)
            {
                fileName = null;
            }
            isRecording = false;
        }
    }

}
