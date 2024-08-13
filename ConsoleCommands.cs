using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;

namespace MatchZy
{
    public partial class MatchZy
    {
        [ConsoleCommand("css_whitelist", "Toggles Whitelisting of players")]
        public void OnWLCommand(CCSPlayerController? player, CommandInfo? command) {            
            if (IsPlayerAdmin(player, "css_whitelist", "@css/config")) {
                isWhitelistRequired = !isWhitelistRequired;
                string WLStatus = isWhitelistRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Whitelist is now {WLStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} Whitelist is now {ChatColors.Green}{WLStatus}{ChatColors.Default}!");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }
        
        [ConsoleCommand("css_ready", "Marks the player ready")]
        public void OnPlayerReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!ready command] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($"{chatPrefix} You are already ready!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = true;
                        player.PrintToChat($"{chatPrefix} You have been marked ready!");
                    }
                    CheckLiveRequired();
                    HandleClanTags();
                }
            }
        }

        [ConsoleCommand("css_unready", "Marks the player unready")]
        public void OnPlayerUnReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!unready command] {player.UserId}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (!playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($"{chatPrefix} You are already unready!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = false;
                        player.PrintToChat($"{chatPrefix} You have been marked unready!");
                    }
                    HandleClanTags();
                }
            }
        }

        [ConsoleCommand("css_stay", "Stays after knife round")]
        public void OnTeamStay(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!stay command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{knifeWinnerName}{ChatColors.Default} has decided to stay!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_switch", "Switch after knife round")]
        public void OnTeamSwitch(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!switch command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.ExecuteCommand("mp_swapteams;");
                    SwapSidesInTeamData(true);
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{knifeWinnerName}{ChatColors.Default} has decided to switch!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_tech", "Pause the match")]
        public void OnTechCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_pause", "Pause the match")]
        public void OnPauseCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_fp", "Pause the matchas an admin")]
        [ConsoleCommand("css_forcepause", "Pause the match as an admin")]
        public void OnForcePauseCommand(CCSPlayerController? player, CommandInfo? command) {            
            ForcePauseMatch(player, command);
        }

        [ConsoleCommand("css_fup", "Pause the matchas an admin")]
        [ConsoleCommand("css_forceunpause", "Pause the match as an admin")]
        public void OnForceUnpauseCommand(CCSPlayerController? player, CommandInfo? command) {            
            ForceUnpauseMatch(player, command);
        }

        [ConsoleCommand("css_unpause", "Unpause the match")]
        public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo? command) {
            if (isMatchLive && isPaused) {
                var pauseTeamName = unpauseData["pauseTeam"];
                if ((string)pauseTeamName == "Admin") {
                    player?.PrintToChat($"{chatPrefix} Match has been paused by an admin, hence it can be unpaused by an admin only.");
                    return;
                }

                string unpauseTeamName = "Admin";
                string remainingUnpauseTeam = "Admin";
                if (player?.TeamNum == 2) {
                    unpauseTeamName = reverseTeamSides["TERRORIST"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["CT"].teamName;
                    if (!(bool)unpauseData["t"]) {
                        unpauseData["t"] = true;
                    }
                    
                } else if (player?.TeamNum == 3) {
                    unpauseTeamName = reverseTeamSides["CT"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["TERRORIST"].teamName;
                    if (!(bool)unpauseData["ct"]) {
                        unpauseData["ct"] = true;
                    }
                } else {
                    return;
                }
                if ((bool)unpauseData["t"] && (bool)unpauseData["ct"]) {
                    Server.PrintToChatAll($"{chatPrefix} Both teams has unpaused the match, resuming the match!");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                } else if (unpauseTeamName == "Admin") {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} has unpaused the match, resuming the match!");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                } else {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} wants to unpause the match. {ChatColors.Green}{remainingUnpauseTeam}{ChatColors.Default}, please write !unpause to confirm.");
                }
                if (!isPaused && pausedStateTimer != null) {
                    pausedStateTimer.Kill();
                    pausedStateTimer = null;
                }
            }
        }

        [ConsoleCommand("css_tac", "Starts a tactical timeout for the requested team")]
        public void OnTacCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            if (matchStarted && isMatchLive) {
                Log($"[.tac command sent via chat] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
                if (player.TeamNum == 2) {
                    Server.ExecuteCommand("timeout_terrorist_start");
                } else if (player.TeamNum == 3) {
                    Server.ExecuteCommand("timeout_ct_start");
                } 
            }
        }

        [ConsoleCommand("css_knife", "Toggles knife round for the match")]
        public void OnKifeCommand(CCSPlayerController? player, CommandInfo? command) {            
            if (IsPlayerAdmin(player, "css_knife", "@css/config")) {
                isKnifeRequired = !isKnifeRequired;
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Knife round is now {knifeStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} Knife round is now {ChatColors.Green}{knifeStatus}{ChatColors.Default}!");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_demo", "Toggles demo recording for the match")]
        public void OnDemoCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_demo", "@css/config"))
            {
                if(!IsHLTVEnabled())
                {
                    //ReplyToUserCommand(player, "Cannot toggle demo recording while CSTV is disabled! Please change map or reload the map.");
                    ReplyToUserCommand(player, "Server randomly crashes due to a CSTV bug since CS2 update on May 2, so CSTV is defaultly off. If you don't mind random crashes and need Demo Recording, please contact TGPRO Admin.");
                    ReplyToUserCommand(player, "We'll bring back CSTV on as soon as Valve fixes the issue.");
                    ReplyToUserCommand(player, "CS2 5月的更新带来的CSTV Bug可能造成服务器随机崩溃，因此服务器默认关闭CSTV。如果您不介意服务器随机崩溃并需要录制Demo，请联系TGPRO管理员。");
                    return;
                }
                if(isMatchLive)
                {
                    ReplyToUserCommand(player, "Cannot toggle demo recording while match is live!");
                    return;
                }
                isDemoRecord = !isDemoRecord;
                string demoStatus = isDemoRecord ? "Enabled" : "Disabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Demo recording is now {demoStatus}!");
                }
                if (isDemoRecord)
                {
                    Server.PrintToChatAll($"{chatPrefix} Demo recording is now {ChatColors.Green}{demoStatus}{ChatColors.Default}!（DEMO录制：已开启）");
                }
                else
                {
                    Server.PrintToChatAll($"{chatPrefix} Demo recording is now {ChatColors.Red}{demoStatus}{ChatColors.Default}!（DEMO录制：已关闭）");
                }
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        public bool IsHLTVEnabled()
        {
            if (ConVar.Find("tv_enable").GetPrimitiveValue<bool>() == true 
                && Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.IsHLTV).Count() > 0)
            {
                return true;
            }
            return false;
        }

        [ConsoleCommand("css_stopdemo", "Stop recording demo")]
        public void OnStopDemoCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_demo", "@css/config"))
            {
                StopDemoRecording();
                Server.PrintToChatAll($"{chatPrefix} Demo recording (Demo录制) has been stopped!");
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }


        [ConsoleCommand("css_readyrequired", "Sets number of ready players required to start the match")]
        public void OnReadyRequiredCommand(CCSPlayerController? player, CommandInfo command) {
            if (IsPlayerAdmin(player, "css_readyrequired", "@css/config")) {
                if (command.ArgCount >= 2) {
                    string commandArg = command.ArgByIndex(1);
                    HandleReadyRequiredCommand(player, commandArg);
                }
                else {
                    string minimumReadyRequiredFormatted = (player == null) ? $"{minimumReadyRequired}" : $"{ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}";
                    ReplyToUserCommand(player, $"Current Ready Required: {minimumReadyRequiredFormatted} .Usage: !readyrequired <number_of_ready_players_required>");
                }                
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_settings", "Shows the current match configuration/settings")]
        public void OnMatchSettingsCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;

            if (IsPlayerAdmin(player, "css_settings", "@css/config")) {
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                string playoutStatus = isPlayOutEnabled ? "Enabled" : "Disabled";
                string demoStatus = isDemoRecord ? "Enabled" : "Disabled";
                player.PrintToChat($"{chatPrefix} Current Settings:");
                player.PrintToChat($"{chatPrefix} Knife: {ChatColors.Green}{knifeStatus}{ChatColors.Default}");
                player.PrintToChat($"{chatPrefix} Minimum Ready Required: {ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}");
                player.PrintToChat($"{chatPrefix} Playout(full rounds): {ChatColors.Green}{playoutStatus}{ChatColors.Default}");
                player.PrintToChat($"{chatPrefix} Demo Recording: {ChatColors.Green}{demoStatus}{ChatColors.Default}");
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_restart", "Restarts the match")]
        public void OnRestartMatchCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_restart", "@css/config")) {
                if (!isPractice) {
                    ResetMatch();
                } else {
                    ReplyToUserCommand(player, "Practice mode is active, cannot restart the match.");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_map", "Changes the map using changelevel")]
        public void OnChangeMapCommand(CCSPlayerController? player, CommandInfo command) {
            if (player == null) return;
            var mapName = command.ArgByIndex(1);
            HandleMapChangeCommand(player, mapName);
        }

        [ConsoleCommand("css_rmap", "Reloads the current map")]
        private void OnMapReloadCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            if (!IsPlayerAdmin(player)) {
                SendPlayerNotAdminMessage(player);
                return;
            }
            string currentMapName = Server.MapName;
            if (Server.IsMapValid(currentMapName)) {
                Server.ExecuteCommand($"bot_kick;map \"{currentMapName}\"");
            } else {
                player.PrintToChat($"{chatPrefix} Cant reload a workshop map!");
            }
        }

        [ConsoleCommand("css_start", "Force starts the match")]
        public void OnStartCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            if (IsPlayerAdmin(player, "css_start", "@css/config")) {
                if (isPractice) {
                    ReplyToUserCommand(player, "Cannot start a match while in practice mode. Please use .noprac command to exit practice mode first!");
                    return;
                }
                if (matchStarted) {
                    player.PrintToChat($"{chatPrefix} Start command cannot be used if match is already started! If you want to unpause, please use .unpause");
                } else {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}Admin{ChatColors.Default} has started the game!");
                    HandleMatchStart();
                    GetSpawns();
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_asay", "Say as an admin")]
        public void OnAdminSay(CCSPlayerController? player, CommandInfo? command) {
            if (command == null) return;
            if (player == null) {
                Server.PrintToChatAll($"{adminChatPrefix} {command.ArgString}");
                return;
            }
            if (!IsPlayerAdmin(player, "css_asay", "@css/chat")) {
                SendPlayerNotAdminMessage(player);
                return;
            }
            string message = "";
            for (int i = 1; i < command.ArgCount; i++) {
                message += command.ArgByIndex(i) + " ";
            }
            Server.PrintToChatAll($"{adminChatPrefix} {message}");
        }

        [ConsoleCommand("reload_admins", "Reload admins of MatchZy")]
        public void OnReloadAdmins(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "reload_admins", "@css/config")) {
                LoadAdmins();
                UpdatePlayersMap();
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_match", "Starts match mode")]
        public void OnMatchCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player, "css_match", "@css/map", "@custom/prac")) {
                SendPlayerNotAdminMessage(player);
                return;
            }

            if (matchStarted) {
                ReplyToUserCommand(player, "Server is already in match mode!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_exitprac", "Starts match mode")]
        public void OnExitPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player, "css_exitprac", "@css/map", "@custom/prac")) {
                SendPlayerNotAdminMessage(player);
                return;
            }

            if (matchStarted) {
                ReplyToUserCommand(player, "MatchZy is already in match mode!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_rcon", "Triggers provided command on the server")]
        public void OnRconCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsPlayerAdmin(player, "css_rcon", "@css/rcon")) {
                SendPlayerNotAdminMessage(player);
                return;
            }
            Server.ExecuteCommand(command.ArgString);
            ReplyToUserCommand(player, "Command sent successfully!");
        }

        [ConsoleCommand("css_help", "Triggers provided command on the server")]
        public void OnHelpCommand(CCSPlayerController? player, CommandInfo? command)
        {
            SendAvailableCommandsMessage(player);
        }

        [ConsoleCommand("css_playout", "Toggles playout (Playing of max rounds)")]
        public void OnPlayoutCommand(CCSPlayerController? player, CommandInfo? command) {            
            if (IsPlayerAdmin(player, "css_playout", "@css/config")) {
                isPlayOutEnabled = !isPlayOutEnabled;
                string playoutStatus = isPlayOutEnabled? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Playout is now {playoutStatus}!");
                    if (isPlayOutEnabled)
                    {
                        ReplyToUserCommand(player, "当前设置：打满所有局数");
                    } else
                    {
                        ReplyToUserCommand(player, "当前设置：赢得13局即结束比赛");
                    }
                } else {
                    if (isPlayOutEnabled)
                    {
                        player.PrintToChat($"{chatPrefix} 当前设置：打满所有局数");
                    } else
                    {
                        player.PrintToChat($"{chatPrefix} 当前设置：赢得13局即结束比赛");
                    }
                }
                
                if (isPlayOutEnabled) {
                    Server.ExecuteCommand("mp_match_can_clinch false");
                } else {
                    Server.ExecuteCommand("mp_match_can_clinch true");
                }
                
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_24r", "Toggles .24r command")]
        public void On24rCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_playout", "@css/config"))
            {
                isPlayOutEnabled = true;
                string playoutStatus = "Enabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Playout is now {playoutStatus}!");
                    ReplyToUserCommand(player, "当前设置：打满所有局数 (Playout all rounds)");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} 当前设置：打满所有局数 (Playout all rounds)");
                }
                Server.ExecuteCommand("mp_match_can_clinch false");
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_13r", "Toggles .13r command")]
        public void On13rCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_playout", "@css/config"))
            {
                isPlayOutEnabled = false;
                string playoutStatus = "Disabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Playout is now {playoutStatus}!");
                    ReplyToUserCommand(player, "当前设置：赢得13局即结束 (Win on 13 rounds)");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} 当前设置：赢得13局即结束 (Win on 13 rounds)");
                }
                Server.ExecuteCommand("mp_match_can_clinch true");
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_testc4", "move c4")]
        public void OnTestC4Command(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_testc4", "@css/config"))
            {
                if (player == null) return;
                if (player.PlayerPawn.IsValid)
                {
                    GiveC4ToARandomPlayer();
                }
            }
        }

        [ConsoleCommand("css_ws", "Weapon Skins")]
        public void OnWSCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player != null && player.IsValid && !player.IsBot)
            {
                player.PrintToChat($"{chatPrefix} Please go to TGPRO Skin Changer {ChatColors.Orange}https://skins.tgpro.net{ChatColors.White} to select Skins/Knifes/Gloves");
                player.PrintToChat($"{chatPrefix} 请使用TGPRO换皮肤网页 {ChatColors.Orange}https://skins-cn.tgpro.net{ChatColors.White} 来选择皮肤、手套。（需要加速器，登录Steam网页)");
                player.PrintToCenter("Hint: Use Steam in-game browswer to open the link");
                player.PrintToConsole(" ");
                player.PrintToConsole("Hint: Use Steam in-game browswer to open the following link");
                player.PrintToConsole(">  https://skins-.tgpro.net");
                player.PrintToConsole(" ");
            }
        }
    }
}
