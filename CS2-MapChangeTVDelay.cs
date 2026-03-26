using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers;

namespace CS2MapChangeStopTV;

public class MapChangeStopTVConfig : BasePluginConfig
{
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
    [JsonPropertyName("MapChangeCooldown")] public float MapChangeCooldown { get; set; } = 120.0f; // 換圖後 120 秒內禁止換圖
}

public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTVConfig>
{
    public override string ModuleName => "CS2-MapChangeStopTV-UltimateFix";
    public override string ModuleVersion => "0.1.2"; // MatchZy 攔截鎖定版
    public override string ModuleAuthor => "Letaryat & Gemini";
    
    public required MapChangeStopTVConfig Config { get; set; }
    private DateTime _lastMapStartTime; // 紀錄地圖載入完成的時間點

    public void OnConfigParsed(MapChangeStopTVConfig config) { Config = config; }

    public override void Load(bool hotReload)
    {
        LogDebug($"CS2-MapChangeStopTV {ModuleVersion} - 載入成功");
        _lastMapStartTime = DateTime.Now;

        // 1. 地圖啟動邏輯：重設計時器並恢復環境
        RegisterListener<OnMapStart>(mapName =>
        {
            _lastMapStartTime = DateTime.Now; // 重啟 120 秒計時
            LogDebug($"地圖 {mapName} 開始，進入 {Config.MapChangeCooldown} 秒保護期。");

            AddTimer(5.0f, () => 
            {
                Server.ExecuteCommand("sv_voiceenable 1"); 
                Server.ExecuteCommand("tv_enable 1");      
                Server.ExecuteCommand("tv_broadcast 0");   
                LogDebug("新地圖載入：已強行恢復 TV 與 語音模組。");
            });
        });

        // 2. 攔截原生換圖指令 (RCON / 控制台)
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        // 3. 【核心】攔截 MatchZy 的 .map 指令 (透過監聽聊天框)
        AddCommandListener("say", ListenerChatCommand, HookMode.Pre);
        AddCommandListener("say_team", ListenerChatCommand, HookMode.Pre);
        
        // 4. 攔截 MatchZy 底層指令 (防止管理員直接用控制台命令)
        AddCommandListener("css_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("css_workshop", ListenerChangeLevel, HookMode.Pre);

        // 5. 監聽比賽結束
        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            LogDebug("結算面板已顯示，1.0 秒後執行暴力卸載程序...");
            AddTimer(1.0f, ForceShutdownTV);
            return HookResult.Continue;
        });

        // 6. 地圖結束最後防線
        RegisterListener<OnMapEnd>(ForceShutdownTV);
    }

    public override void Unload(bool hotReload)
    {
        ForceShutdownTV();
    }

    // 處理聊天框的 .map / !map
    public HookResult ListenerChatCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return HookResult.Continue;

        // 取得聊天內容並清理空白
        string msg = info.GetArg(1).Trim().ToLower();

        // 如果開頭是 MatchZy 的換圖符號
        if (msg.StartsWith(".map") || msg.StartsWith("!map"))
        {
            return CheckCooldownAndProcess(player);
        }

        return HookResult.Continue;
    }

    // 處理控制台直接輸入的換圖指令
    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        return CheckCooldownAndProcess(player);
    }

    // 統一的時間檢查邏輯
    private HookResult CheckCooldownAndProcess(CCSPlayerController? player)
    {
        double secondsSinceStart = (DateTime.Now - _lastMapStartTime).TotalSeconds;

        if (secondsSinceStart < Config.MapChangeCooldown)
        {
            float timeLeft = Config.MapChangeCooldown - (float)secondsSinceStart;
            
            if (player != null)
            {
                // 給管理員明確的提示
                player.PrintToChat($" \x02[ T W ] \x01換圖保護中！請等待 \x04{timeLeft:F0} \x01秒後再使用 \x02.map\x01。");
            }

            LogDebug($"攔截換圖指令：冷卻中 (剩餘 {timeLeft:F0} 秒)");
            return HookResult.Handled; // 徹底擋下指令，MatchZy 不會收到
        }

        LogDebug("冷卻已過，執行換圖前置清理...");
        ForceShutdownTV();
        return HookResult.Continue;
    }

    public void LogDebug(string message)
    {
        if (Config.Debug == false) { return; }
        Logger.LogInformation($"[StopTV-Fix] {message}");
    }

    public void ForceShutdownTV()
    {
        Server.ExecuteCommand("tv_stoprecord");
        Server.ExecuteCommand("tv_broadcast 0");
        Server.ExecuteCommand("sv_voiceenable 0");
        Server.ExecuteCommand("tv_enable 0");
        LogDebug("環境已清空：錄影、廣播、語音、TV 模組已全數強行卸載。");
    }
}
