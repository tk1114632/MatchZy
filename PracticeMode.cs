﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory;

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Mime;
using System.Text.Json.Serialization;



namespace MatchZy
{
    public class Position
    {

        public Vector PlayerPosition { get; private set; }
        public QAngle PlayerAngle { get; private set; }
        public Position(Vector playerPosition, QAngle playerAngle)
        {
            // Create deep copies of the Vector and QAngle objects
            PlayerPosition = new Vector(playerPosition.X, playerPosition.Y, playerPosition.Z);
            PlayerAngle = new QAngle(playerAngle.X, playerAngle.Y, playerAngle.Z);
        }
    }

    public partial class MatchZy
    {
        int maxLastGrenadesSavedLimit = 512;
        Dictionary<int, List<GrenadeThrownData>> lastGrenadesData = new();
        Dictionary<int, Dictionary<string, GrenadeThrownData>> nadeSpecificLastGrenadeData = new();
        Dictionary<int, DateTime> lastGrenadeThrownTime = new();
        Dictionary<int, PlayerPracticeTimer> playerTimers = new();

        public Dictionary<byte, List<Position>> spawnsData = new Dictionary<byte, List<Position>> {
            { (byte)CsTeam.CounterTerrorist, new List<Position>() },
            { (byte)CsTeam.Terrorist, new List<Position>() }
        };

        public Dictionary<byte, List<Position>> spawnsDataCoach = new Dictionary<byte, List<Position>> {
            { (byte)CsTeam.CounterTerrorist, new List<Position>() },
            { (byte)CsTeam.Terrorist, new List<Position>() }
        };

        public const string practiceCfgPath = "MatchZy/prac.cfg";

        // This map stores the bots which are being used in prac (probably spawned using .bot). Key is the userid of the bot.
        public Dictionary<int, Dictionary<string, object>> pracUsedBots = new Dictionary<int, Dictionary<string, object>>();

        public bool isSpawningBot;

        public List<int> noFlashList = new List<int>();

        public void StartPracticeMode()
        {
            if (matchStarted) return;
            isPractice = true;
            isWarmup = false;
            readyAvailable = false;

            var absolutePath = Path.Join(Server.GameDirectory + "/csgo/cfg", practiceCfgPath);

            if (File.Exists(Path.Join(Server.GameDirectory + "/csgo/cfg", practiceCfgPath)))
            {
                Log($"[StartWarmup] Starting Practice Mode! Executing Practice CFG from {practiceCfgPath}");
                Server.ExecuteCommand($"exec {practiceCfgPath}");
            }
            else
            {
                Log($"[StartWarmup] Starting Practice Mode! Practice CFG not found in {absolutePath}, using default CFG!");
                Server.ExecuteCommand("""sv_cheats "true"; mp_force_pick_time "0"; bot_quota "0"; sv_showimpacts "1"; mp_limitteams "0"; sv_deadtalk "true"; sv_full_alltalk "true"; sv_ignoregrenaderadio "false"; mp_forcecamera "0"; sv_grenade_trajectory_prac_pipreview "true"; sv_grenade_trajectory_prac_trailtime "3"; sv_infinite_ammo "1"; weapon_auto_cleanup_time "15"; weapon_max_before_cleanup "30"; mp_buy_anywhere "1"; mp_maxmoney "9999999"; mp_startmoney "9999999";""");
                Server.ExecuteCommand("""mp_weapons_allow_typecount "-1"; mp_death_drop_breachcharge "false"; mp_death_drop_defuser "false"; mp_death_drop_taser "false"; mp_drop_knife_enable "true"; mp_death_drop_grenade "0"; ammo_grenade_limit_total "5"; mp_defuser_allocation "2"; mp_free_armor "2"; mp_ct_default_grenades "weapon_incgrenade weapon_hegrenade weapon_smokegrenade weapon_flashbang weapon_decoy"; mp_ct_default_primary "weapon_m4a1";""");
                Server.ExecuteCommand("""mp_t_default_grenades "weapon_molotov weapon_hegrenade weapon_smokegrenade weapon_flashbang weapon_decoy"; mp_t_default_primary "weapon_ak47"; mp_warmup_online_enabled "true"; mp_warmup_pausetimer "1"; mp_warmup_start; bot_quota_mode fill; mp_solid_teammates 2; mp_autoteambalance false; mp_teammates_are_enemies false;""");
            }
            GetSpawns();
            Server.PrintToChatAll($"{chatPrefix} Prac mode loaded!");
            Server.PrintToChatAll($"{chatPrefix} Available commands (可用指令):");
	        Server.PrintToChatAll($"{chatPrefix} \x10.spawn .ctspawn .tspawn .bot .nobots .dry .noprac .throw");
            //Server.PrintToChatAll($"{chatPrefix} \x10.loadnade <name>, .savenade <name>, .importnade <code> .listnades <optional filter>");
        }
        public class CustomSpawnPoint
        {
            [JsonPropertyName("map")]
            public string? Map { get; set; }
            [JsonPropertyName("team")]
            public CsTeam Team { get; set; }
            [JsonPropertyName("origin")]
            public string? Origin { get; set; }
            [JsonPropertyName("angle")]
            public string? Angle { get; set; }
        }

        public List<CustomSpawnPoint> _coachSpawnPoints = new()!;
        public Dictionary<byte, Position> theCoachSpawn = new()!;

        public void LoadCoachSpawns()
        {
            _coachSpawnPoints.Clear();
            /*spawnsDataCoach = new Dictionary<byte, List<Position>> {
                        { (byte)CsTeam.CounterTerrorist, new List<Position>() },
                        { (byte)CsTeam.Terrorist, new List<Position>() }
                    };*/
            theCoachSpawn.Clear();
            var absolutePath = Path.Join(Server.GameDirectory + "/csgo/cfg", "MatchZy/coachspawns.json");
            if (File.Exists(absolutePath))
            {
                var jsonString = File.ReadAllText(absolutePath);
                var mapName = Server.MapName.ToLower();
                _coachSpawnPoints = JsonSerializer.Deserialize<List<CustomSpawnPoint>>(jsonString);

                if (_coachSpawnPoints != null)
                {
                    foreach (var spawn in _coachSpawnPoints)
                    {
                        if (spawn.Map == mapName)
                        {
                            var origin = spawn.Origin?.Split(' ');
                            var angle = spawn.Angle?.Split(' ');
                            if (origin != null && angle != null)
                            {
                                var position = new Position(new Vector(float.Parse(origin[0]), float.Parse(origin[1]), float.Parse(origin[2])), new QAngle(float.Parse(angle[0]), float.Parse(angle[1]), float.Parse(angle[2])));
                                //spawnsDataCoach[(byte)spawn.Team].Add(position);
                                Log("Coach (Team " + spawn.Team.ToString() + ") spawn added: " + position.PlayerPosition.X + " " + position.PlayerPosition.Y + " " + position.PlayerPosition.Z);
                                theCoachSpawn[(byte)spawn.Team] = position;
                            }
                        }
                    }
                }
            }
            else
            {
                Log($"[LoadCoachSpawns] coachspawns.json not found in {absolutePath}");
            }
        }
        public void GetSpawns()
        {
            // Resetting spawn data to avoid any glitches
            spawnsData = new Dictionary<byte, List<Position>> {
                        { (byte)CsTeam.CounterTerrorist, new List<Position>() },
                        { (byte)CsTeam.Terrorist, new List<Position>() }
                    };
            /*spawnsDataCoach = new Dictionary<byte, List<Position>> {
                        { (byte)CsTeam.CounterTerrorist, new List<Position>() },
                        { (byte)CsTeam.Terrorist, new List<Position>() }
                    };*/

            int minPriority = 1;

            var spawnsct = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist");
            foreach (var spawn in spawnsct)
            {
                Log($"CT spawns: Spawn priority: {spawn.Priority}; IsValid: {spawn.Enabled}");
                if (spawn.IsValid && spawn.Enabled && spawn.Priority < minPriority)
                {
                    minPriority = spawn.Priority;
                }
            }

            foreach (var spawn in spawnsct)
            {
                if (spawn.IsValid && spawn.Enabled && spawn.Priority == minPriority)
                {
                    spawnsData[(byte)CsTeam.CounterTerrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                }
                /*else if (spawn.IsValid && spawn.Enabled && spawn.Priority > minPriority)
                    {
                    spawnsDataCoach[(byte)CsTeam.CounterTerrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                    Log($"CT Coach spawn added: {spawn.CBodyComponent?.SceneNode?.AbsOrigin.X} {spawn.CBodyComponent?.SceneNode?.AbsOrigin.Y} {spawn.CBodyComponent?.SceneNode?.AbsOrigin.Z}");

                }*/
            }

            var spawnst = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist");
            foreach (var spawn in spawnst)
            {
                Log($"T spawns: Spawn priority: {spawn.Priority}; IsValid: {spawn.Enabled}");
                if (spawn.IsValid && spawn.Enabled && spawn.Priority == minPriority)
                {
                    spawnsData[(byte)CsTeam.Terrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                }
                /*else if (spawn.IsValid && spawn.Enabled && spawn.Priority > minPriority)
                {
                    spawnsDataCoach[(byte)CsTeam.Terrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                    Log($"T Coach spawn added: {spawn.CBodyComponent?.SceneNode?.AbsOrigin.X} {spawn.CBodyComponent?.SceneNode?.AbsOrigin.Y} {spawn.CBodyComponent?.SceneNode?.AbsOrigin.Z}");
                }*/
            }

            LoadCoachSpawns();
        }

        private void HandleSpawnCommand(CCSPlayerController? player, string commandArg, byte teamNum, string command)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            if (teamNum != 2 && teamNum != 3) return;
            if (!string.IsNullOrWhiteSpace(commandArg))
            {
                if (int.TryParse(commandArg, out int spawnNumber) && spawnNumber >= 1)
                {
                    // Adjusting the spawnNumber according to the array index.
                    spawnNumber -= 1;
                    if (spawnsData.ContainsKey(teamNum) && spawnsData[teamNum].Count <= spawnNumber) return;
                    player.PlayerPawn.Value?.Teleport(spawnsData[teamNum][spawnNumber].PlayerPosition, spawnsData[teamNum][spawnNumber].PlayerAngle, new Vector(0, 0, 0));
                    ReplyToUserCommand(player, $"Moved to spawn: {spawnNumber+1}/{spawnsData[teamNum].Count}");
                }
                else
                {
                    ReplyToUserCommand(player, $"Invalid value for {command} command. Please specify a valid non-negative number. Usage: !{command} <number>");
                    return;
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !{command} <number>");
            }
        }

        private string GetNadeType(string nadeName)
        {
            switch (nadeName)
            {
                case "weapon_flashbang":
                    return "Flash";
                case "weapon_smokegrenade":
                    return "Smoke";
                case "weapon_hegrenade":
                    return "HE";
                case "weapon_decoy":
                    return "Decoy";
                case "weapon_molotov":
                    return "Molly";
                case "weapon_incgrenade":
                    return "Molly";
                default:
                    return "";
            }
        }

        private void HandleSaveNadeCommand(CCSPlayerController? player, string saveNadeName)
        {
            if (!isPractice || !IsPlayerValid(player)) return;

            if (!string.IsNullOrWhiteSpace(saveNadeName))
            {
                // Split string into 2 parts
                string[] lineupUserString = saveNadeName.Split(' ');
                string lineupName = lineupUserString[0];
                string lineupDesc = string.Join(" ", lineupUserString, 1, lineupUserString.Length - 1);

                // Get player info: steamid, pos, ang
                string playerSteamID = player.SteamID.ToString();
                QAngle playerAngle = player.PlayerPawn.Value.EyeAngles;
                Vector playerPos = player.Pawn.Value.CBodyComponent!.SceneNode.AbsOrigin;
                string currentMapName = Server.MapName;
                string nadeType = GetNadeType(player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.DesignerName);

                // Define the file path
                string savednadesfileName = "MatchZy/savednades.json";
                string savednadesPath = Path.Join(Server.GameDirectory + "/csgo/cfg", savednadesfileName);

                // Check if the file exists, if not, create it with an empty JSON object
                if (!File.Exists(savednadesPath))
                {
                    File.WriteAllText(savednadesPath, "{}");
                }

                try
                {
                    // Read existing JSON content
                    string existingJson = File.ReadAllText(savednadesPath);

                    // Deserialize the existing JSON content
                    var savedNadesDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(existingJson)
                                        ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                    // Check if the lineup name already exists for the given SteamID
                    if (savedNadesDict.ContainsKey(playerSteamID) && savedNadesDict[playerSteamID].ContainsKey(lineupName))
                    {
                        // Lineup already exists, reply to the user and return
                        ReplyToUserCommand(player, $"Lineup already exists! Please use a different name or use .delnade <nade>");
                        return;
                    }

                    // Update or add the new lineup information
                    if (!savedNadesDict.ContainsKey(playerSteamID))
                    {
                        savedNadesDict[playerSteamID] = new Dictionary<string, Dictionary<string, string>>();
                    }

                    savedNadesDict[playerSteamID][lineupName] = new Dictionary<string, string>
                    {
                        { "LineupPos", $"{playerPos.X} {playerPos.Y} {playerPos.Z}" },
                        { "LineupAng", $"{playerAngle.X} {playerAngle.Y} {playerAngle.Z}" },
                        { "Desc", lineupDesc },
                        { "Map", currentMapName },
                        { "Type", nadeType }
                    };

                    // Serialize the updated dictionary back to JSON
                    string updatedJson = JsonSerializer.Serialize(savedNadesDict, new JsonSerializerOptions { WriteIndented = true });

                    // Write the updated JSON content back to the file
                    File.WriteAllText(savednadesPath, updatedJson);

                    //Reply to user
                    ReplyToUserCommand(player, $" \x0DLineup \x06'{lineupName}' \x0Dsaved successfully!");
					player.PrintToCenter($"Lineup '{lineupName}' saved successfully!");
					ReplyToUserCommand(player, $" \x0DLineup Code: \x06{lineupName} {playerPos} {playerAngle}");
                }
                catch (JsonException ex)
                {
                    Log($"Error handling JSON: {ex.Message}");
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: .savenade <name>");
            }
        }

        private void HandleDeleteNadeCommand(CCSPlayerController? player, string saveNadeName)
        {
            if (!isPractice || player == null) return;

            if (!string.IsNullOrWhiteSpace(saveNadeName))
            {
                // Grab player steamid
                string playerSteamID = player.SteamID.ToString();

                // Define the file path
                string savednadesfileName = "MatchZy/savednades.json";
                string savednadesPath = Path.Join(Server.GameDirectory + "/csgo/cfg", savednadesfileName);

                try
                {
                    // Read existing JSON content
                    string existingJson = File.ReadAllText(savednadesPath);

                    //Console.WriteLine($"Existing JSON Content: {existingJson}");

                    // Deserialize the existing JSON content
                    var savedNadesDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(existingJson)
                                        ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                    // Check if the lineup exists for the given SteamID and name
                    if (savedNadesDict.ContainsKey(playerSteamID) && savedNadesDict[playerSteamID].ContainsKey(saveNadeName))
                    {
                        // Remove the specified lineup
                        savedNadesDict[playerSteamID].Remove(saveNadeName);

                        // Serialize the updated dictionary back to JSON
                        string updatedJson = JsonSerializer.Serialize(savedNadesDict, new JsonSerializerOptions { WriteIndented = true });

                        // Write the updated JSON content back to the file
                        File.WriteAllText(savednadesPath, updatedJson);

                        ReplyToUserCommand(player, $"Lineup '{saveNadeName}' deleted successfully.");
                    }
                    else
                    {
                        ReplyToUserCommand(player, $"Lineup '{saveNadeName}' not found!");
                    }
                }
                catch (JsonException ex)
                {
                    Log($"Error handling JSON: {ex.Message}");
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: .delnade <name>");
            }
        }

        private void HandleImportNadeCommand(CCSPlayerController? player, string saveNadeCode)
        {
            if (!isPractice || player == null) return;

            if (!string.IsNullOrWhiteSpace(saveNadeCode))
            {
                try
                {
                    // Split the code into parts
                    string[] parts = saveNadeCode.Split(' ');

                    // Check if there are enough parts
                    if (parts.Length == 7)
                    {
                        // Extract name, pos, and ang from the parts
                        string lineupName = parts[0].Trim();
                        string[] posAng = parts.Skip(1).Select(p => p.Replace(",", "")).ToArray(); // Replace ',' with '' for proper parsing

                        // Get player info: steamid
                        string playerSteamID = player.SteamID.ToString();
                        string currentMapName = Server.MapName;

                        // Define the file path
                        string savednadesfileName = "MatchZy/savednades.json";
                        string savednadesPath = Path.Join(Server.GameDirectory + "/csgo/cfg", savednadesfileName);

                        // Read existing JSON content
                        string existingJson = File.ReadAllText(savednadesPath);

                        //Console.WriteLine($"Existing JSON Content: {existingJson}");

                        // Deserialize the existing JSON content
                        var savedNadesDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(existingJson)
                                            ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                        // Check if the lineup name already exists for the given SteamID
                        if (savedNadesDict.ContainsKey(playerSteamID) && savedNadesDict[playerSteamID].ContainsKey(lineupName))
                        {
                            // Lineup already exists, reply to the user and return
                            ReplyToUserCommand(player, $"Lineup '{lineupName}' already exists! Please use a different name or use .delnade <nade>");
                            return;
                        }

                        // Update or add the new lineup information
                        if (!savedNadesDict.ContainsKey(playerSteamID))
                        {
                            savedNadesDict[playerSteamID] = new Dictionary<string, Dictionary<string, string>>();
                        }

                        savedNadesDict[playerSteamID][lineupName] = new Dictionary<string, string>
                        {
                            { "LineupPos", $"{posAng[0]} {posAng[1]} {posAng[2]}" },
                            { "LineupAng", $"{posAng[3]} {posAng[4]} {posAng[5]}" },
                            { "Desc", "" },
                            { "Map", currentMapName }
                        };

                        // Serialize the updated dictionary back to JSON
                        string updatedJson = JsonSerializer.Serialize(savedNadesDict, new JsonSerializerOptions { WriteIndented = true });

                        // Write the updated JSON content back to the file
                        File.WriteAllText(savednadesPath, updatedJson);

                        ReplyToUserCommand(player, $"Lineup '{lineupName}' imported and saved successfully.");
                    }
                    else
                    {
                        ReplyToUserCommand(player, $"Invalid code format. Please provide a valid code with name, pos, and ang.");
                    }
                }
                catch (JsonException ex)
                {
                    Log($"Error handling JSON: {ex.Message}");
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: .importnade <code>");
            }
        }

        private void HandleListNadesCommand(CCSPlayerController? player, string nadeFilter)
        {
            if (!isPractice || player == null) return;

            // Define the file path
            string savednadesfileName = "MatchZy/savednades.json";
            string savednadesPath = Path.Join(Server.GameDirectory + "/csgo/cfg", savednadesfileName);

            try
            {
                // Read existing JSON content
                string existingJson = File.ReadAllText(savednadesPath);

                //Console.WriteLine($"Existing JSON Content: {existingJson}");

                // Deserialize the existing JSON content
                var savedNadesDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(existingJson)
                                    ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                ReplyToUserCommand(player, $"\x0D-----All Saved Lineups for \x06{Server.MapName}\x0D-----");

                // List lineups for the specified player
                ListLineups(player, "default", Server.MapName, savedNadesDict, nadeFilter);

                // List lineups for the current player
                ListLineups(player, player.SteamID.ToString(), Server.MapName, savedNadesDict, nadeFilter);
            }
            catch (JsonException ex)
            {
                Log($"Error handling JSON: {ex.Message}");
                ReplyToUserCommand(player, $"Error handling JSON. Please check the server logs.");
            }
        }

        private void ListLineups(CCSPlayerController player, string steamID, string mapName, Dictionary<string, Dictionary<string, Dictionary<string, string>>> savedNadesDict, string nadeFilter)
        {
            if (savedNadesDict.ContainsKey(steamID))
            {
                foreach (var kvp in savedNadesDict[steamID])
                {
                    // Check if a filter is provided, and if so, apply the filter
                    if ((string.IsNullOrWhiteSpace(nadeFilter) || kvp.Key.Contains(nadeFilter, StringComparison.OrdinalIgnoreCase))
                        && kvp.Value.ContainsKey("Map") && kvp.Value["Map"] == mapName)
                    {
                        // Format and reply with the lineup name
                        ReplyToUserCommand(player, $"\x06[{kvp.Value["Type"]}] \x0D.loadnade \x06{kvp.Key}");
                    }
                }
            }
            else
            {
                ReplyToUserCommand(player, $"No saved lineups found for the specified SteamID: ({steamID}).");
            }
        }


        private void HandleLoadNadeCommand(CCSPlayerController? player, string loadNadeName)
        {
            if (!isPractice || player == null) return;

            if (!string.IsNullOrWhiteSpace(loadNadeName))
            {
                // Get player info: steamid
                string playerSteamID = player.SteamID.ToString();

                // Define the file path
                string savednadesfileName = "MatchZy/savednades.json";
                string savednadesPath = Path.Join(Server.GameDirectory + "/csgo/cfg", savednadesfileName);

                try
                {
                    // Read existing JSON content
                    string existingJson = File.ReadAllText(savednadesPath);

                    //Console.WriteLine($"Existing JSON Content: {existingJson}");

                    // Deserialize the existing JSON content
                    var savedNadesDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(existingJson)
                                        ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                    bool lineupFound = false;
                    bool lineupOnWrongMap = false;

                    // Check for the lineup in the player's steamID and the fixed steamID
                    foreach (string currentSteamID in new[] { playerSteamID, "default" })
                    {
                        if (savedNadesDict.ContainsKey(currentSteamID) && savedNadesDict[currentSteamID].ContainsKey(loadNadeName))
                        {
                            var lineupInfo = savedNadesDict[currentSteamID][loadNadeName];

                            // Check if the lineup contains the "Map" key and if it matches the current map
                            if (lineupInfo.ContainsKey("Map") && lineupInfo["Map"] == Server.MapName)
                            {
                                // Extract position and angle from the lineup information
                                string[] posArray = lineupInfo["LineupPos"].Split(' ');
                                string[] angArray = lineupInfo["LineupAng"].Split(' ');

                                // Parse position and angle
                                Vector loadedPlayerPos = new Vector(float.Parse(posArray[0]), float.Parse(posArray[1]), float.Parse(posArray[2]));
                                QAngle loadedPlayerAngle = new QAngle(float.Parse(angArray[0]), float.Parse(angArray[1]), float.Parse(angArray[2]));

                                // Teleport player
                                player.PlayerPawn.Value?.Teleport(loadedPlayerPos, loadedPlayerAngle, new Vector(0, 0, 0));

                                // Change player inv slot
                                switch (lineupInfo["Type"])
                                {
                                    case "Flash":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot7");
                                        break;
                                    case "Smoke":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot8");
                                        break;
                                    case "HE":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot6");
                                        break;
                                    case "Decoy":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot9");
                                        break;
                                    case "Molly":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot10");
                                        break;
                                    case "":
                                        NativeAPI.IssueClientCommand((int)player.Index! - 1, "slot8");
                                        break;
                                }

                                // Extract description, if available
                                string lineupDesc = lineupInfo.ContainsKey("Desc") ? lineupInfo["Desc"] : null;

                                // Print messages
                                ReplyToUserCommand(player, $" \x0D Lineup \x06'{loadNadeName}' \x0Dloaded successfully!");

                                if (!string.IsNullOrWhiteSpace(lineupDesc))
                                {
                                    player.PrintToCenter($"{lineupDesc}");
                                    ReplyToUserCommand(player, $" \x0D Description: \x06'{lineupDesc}'");
                                }

                                lineupFound = true;
                                break;
                            }
                            else
                            {
                                ReplyToUserCommand(player, $"Nade '{loadNadeName}' not found on the current map!");
                                lineupOnWrongMap = true;
                            }
                        }
                    }

                    if (!lineupFound && !lineupOnWrongMap)
                    {
                        // Lineup not found
                        ReplyToUserCommand(player, $"Nade '{loadNadeName}' not found!");
                    }
                }
                catch (JsonException ex)
                {
                    Log($"Error handling JSON: {ex.Message}");
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Nade not found! Usage: .loadnade <name>");
            }
        }

        [ConsoleCommand("css_god", "Sets Infinite health for player")]
        public void OnGodCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;
	    
			int currentHP = player.PlayerPawn.Value.Health;
			
			if(currentHP > 100)
			{
				player.PlayerPawn.Value.Health = 100;
				ReplyToUserCommand(player, $"God mode disabled!");
				return;
			}
			else
			{
				player.PlayerPawn.Value.Health = 2147483647; // max 32bit int
				ReplyToUserCommand(player, $"God mode enabled!");
				return;
			}
        }

        [ConsoleCommand("css_prac", "Starts practice mode")]
        public void OnPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player, "css_prac", "@css/map", "@custom/prac")) {
                SendPlayerNotAdminMessage(player);
                return;
            }

            if (matchStarted)
            {
                ReplyToUserCommand(player, "Practice Mode cannot be started when a match has been started!");
                ReplyToUserCommand(player, "比赛已开始，无法进入Prac模式；请先输入 .endmatch 退出比赛");
                return;
            }
	    
			if (isPractice)
			{
                //StartMatchMode();
                ReplyToUserCommand(player, "Practice Mode is already ON, use .noprac/.setup to start match mode!");
                ReplyToUserCommand(player, "已处于Prac模式，输入 .noprac/.setup 退出Prac模式");
                return;
			}
	
			StartPracticeMode();
        }

        [ConsoleCommand("css_dry", "Starts practice mode")]
        public void OnDryCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (matchStarted)
            {
                ReplyToUserCommand(player, "Dry run is disabled when a match has been started!");
                ReplyToUserCommand(player, "比赛模式下不能 .dry 跑图");
                return;
            }

            if (isPractice)
            {
                DryrunOnce();
                Server.PrintToChatAll($"{chatPrefix} Restarting round with freezetime; use .undry to go back to Prac Mode!");
                Server.PrintToChatAll($"{chatPrefix} 正在 .dry 刷新（冻结时间、购买限制）; 输入 .undry 返回Prac模式");
            }
            else
            {
                ReplyToUserCommand(player, "Dry run is only available in Prac Mode!");
                ReplyToUserCommand(player, "只能在Prac模式下使用 .dry 命令");
            }
            return;
        }

        [ConsoleCommand("css_rr", "restart round in 1 sec")]
        public void OnRRCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (matchStarted)
            {
                ReplyToUserCommand(player, "Restart round is disabled when a match has been started!");
                ReplyToUserCommand(player, "比赛模式下不能 .rr");
                return;
            }

            if (isPractice)
            {
                Server.ExecuteCommand("mp_restartgame 1");
                Server.PrintToChatAll($"{chatPrefix} Restarting round...");
                Server.PrintToChatAll($"{chatPrefix} 正在刷新...");
            }
            else
            {
                ReplyToUserCommand(player, "RR is only available in Prac Mode!");
                ReplyToUserCommand(player, "只能在Prac模式下使用 .rr 命令");
            }
            return;
        }

        [ConsoleCommand("css_undry", "Starts practice mode")]
        public void OnUnDryCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (matchStarted)
            {
                ReplyToUserCommand(player, "Dry run is disabled when a match has been started!");
                return;
            }

            if (isPractice)
            {
                ExitDryrun();
                Server.PrintToChatAll($"{chatPrefix} Exiting Dry Run; No limits to buy nades.");
                Server.PrintToChatAll($"{chatPrefix} 退出 .dry 回到Prac模式");
            }
            else
            {
                ReplyToUserCommand(player, "Dry run is only available in Prac Mode!");
                ReplyToUserCommand(player, "只能在Prac模式下使用 .undry 命令");
            }
            return;
        }

        [ConsoleCommand("css_spawn", "Teleport to provided spawn")]
        public void OnSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, player.TeamNum, "spawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !spawn <round>");
            }
        }

        [ConsoleCommand("css_watchme", "Switches all other players to spectator")]
        public void OnFASCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;

            SideSwitchCommand(player, CsTeam.None);
        }

        [ConsoleCommand("css_noflash", "Disables flash effect for the player")]
        public void OnNoFlashCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null || player.UserId == null) return;

            int userId = player.UserId.Value;

            if (noFlashList.Contains(userId))
            {
                noFlashList.Remove(userId);
                ReplyToUserCommand(player, "Disabled noflash.");
            }
            else
            {
                noFlashList.Add(userId);
                ReplyToUserCommand(player, "Enabled noflash. Use .noflash again to disable.");
                Server.NextFrame(() => KillFlashEffect(player));
            }

        }

        // CsTeam.None is a special value to mean force all other players to spectator
        private void SideSwitchCommand(CCSPlayerController player, CsTeam team)
        {
            if (team > CsTeam.None)
            {
                if (player.TeamNum == (byte)CsTeam.Spectator)
                {
                    ReplyToUserCommand(player, "Switching to a team from spectator is currently broken, use the team menu.");
                    return;
                }
                player.ChangeTeam(team);
                return;
            }
            Utilities.GetPlayers().ForEach((x) => {
                if (x.IsValid && !x.IsBot && x.UserId != player.UserId && x.Connected == PlayerConnectedState.PlayerConnected)
                {
                    x.ChangeTeam(CsTeam.Spectator);
                }
            });
        }

        [ConsoleCommand("css_ctspawn", "Teleport to provided CT spawn")]
        public void OnCtSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, (byte)CsTeam.CounterTerrorist, "ctspawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !ctspawn <round>");
            }
        }

        [ConsoleCommand("css_tspawn", "Teleport to provided T spawn")]
        public void OnTSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, (byte)CsTeam.Terrorist, "tspawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !ctspawn <round>");
            }
        }

        [ConsoleCommand("css_bot", "Spawns a bot at the player's position")]
        public void OnBotCommand(CCSPlayerController? player, CommandInfo? command)
        {
            AddBot(player, false);
        }

        [ConsoleCommand("css_cbot", "Spawns a crouched bot at the player's position")]
        [ConsoleCommand("css_crouchbot", "Spawns a crouched bot at the player's position")]
        public void OnCrouchBotCommand(CCSPlayerController? player, CommandInfo? command)
        {
            AddBot(player, true);
        }

        [ConsoleCommand("css_boost", "Spawns a bot at the player's position and boost the player on it")]
        public void OnBoostBotCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice) return;
            AddBot(player, false);
            AddTimer(0.2f, () => ElevatePlayer(player));
        }

        [ConsoleCommand("css_crouchboost", "Spawns a crouched bot at the player's position and boost the player on it")]
        public void OnCrouchBoostBotCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice) return;
            AddBot(player, true);
            AddTimer(0.2f, () => ElevatePlayer(player));
        }

        private void AddBot(CCSPlayerController? player, bool crouch)
        {
            try
            {
                if (!isPractice || player == null || !player.IsValid || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null) return;
                CCSPlayer_MovementServices movementService = new(player.PlayerPawn.Value.MovementServices!.Handle);

                if ((int)movementService.DuckAmount == 1)
                {
                    // Player was crouching while using .bot command
                    crouch = true;
                }
                isSpawningBot = true;
                // !bot/.bot command is made using a lot of workarounds, as there is no direct way to create a bot entity and spawn it in CSSharp
                // Hence there can be some issues with this approach. This will be revamped when we will be able to fake clients.
                if (player.TeamNum == (byte)CsTeam.CounterTerrorist)
                {
                    Server.ExecuteCommand("bot_join_team T");
                    Server.ExecuteCommand("bot_add_t");
                }
                else if (player.TeamNum == (byte)CsTeam.Terrorist)
                {
                    Server.ExecuteCommand("bot_join_team CT");
                    Server.ExecuteCommand("bot_add_ct");
                }

                // Once bot is added, we teleport it to the requested position
                AddTimer(0.1f, () => SpawnBot(player, crouch));
                Server.ExecuteCommand("bot_stop 1");
                Server.ExecuteCommand("bot_freeze 1");
                Server.ExecuteCommand("bot_zombie 1");
            }
            catch (JsonException ex)
            {
                Log($"[AddBot - FATAL] Error: {ex.Message}");
            }
        }

        private void SpawnBot(CCSPlayerController botOwner, bool crouch)
        {
            try
            {
                if (!IsPlayerValid(botOwner)) return;
                var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
                bool unusedBotFound = false;
                foreach (var tempPlayer in playerEntities)
                {
                    if (!IsPlayerValid(tempPlayer)) continue;
                    if (!tempPlayer.IsBot || tempPlayer.IsHLTV) continue;
                    if (tempPlayer.UserId.HasValue)
                    {
                        if (!pracUsedBots.ContainsKey(tempPlayer.UserId.Value) && unusedBotFound)
                        {
                            Log($"UNUSED BOT FOUND: {tempPlayer.UserId.Value} EXECUTING: kickid {tempPlayer.UserId.Value}");
                            // Kicking the unused bot. We have to do this because bot_add_t/bot_add_ct may add multiple bots but we need only 1, so we kick the remaining unused ones
                            Server.ExecuteCommand($"kickid {tempPlayer.UserId.Value}");
                            continue;
                        }
                        if (pracUsedBots.ContainsKey(tempPlayer.UserId.Value))
                        {
                            continue;
                        }
                        pracUsedBots[tempPlayer.UserId.Value] = new Dictionary<string, object>();

                        Position botOwnerPosition = new Position(botOwner.PlayerPawn.Value!.CBodyComponent?.SceneNode?.AbsOrigin!, botOwner.PlayerPawn.Value!.CBodyComponent?.SceneNode?.AbsRotation!);
                        // Add key-value pairs to the inner dictionary
                        pracUsedBots[tempPlayer.UserId.Value]["controller"] = tempPlayer;
                        pracUsedBots[tempPlayer.UserId.Value]["position"] = botOwnerPosition;
                        pracUsedBots[tempPlayer.UserId.Value]["owner"] = botOwner;
                        pracUsedBots[tempPlayer.UserId.Value]["crouchstate"] = crouch;

                        if (crouch)
                        {
                            CCSPlayer_MovementServices movementService = new(tempPlayer.PlayerPawn.Value!.MovementServices!.Handle);
                            AddTimer(0.1f, () => movementService.DuckAmount = 1);
                            AddTimer(0.2f, () => tempPlayer.PlayerPawn.Value!.Bot!.IsCrouching = true);
                        }

                        tempPlayer.PlayerPawn.Value!.Teleport(botOwnerPosition.PlayerPosition, botOwnerPosition.PlayerAngle, new Vector(0, 0, 0));
                        TemporarilyDisableCollisions(botOwner, tempPlayer);
                        unusedBotFound = true;
                    }
                }
                if (!unusedBotFound)
                {
                    // Server.PrintToChatAll($"{chatPrefix} Cannot add bots, the team is full! Use .nobots to remove the current bots.");
                    Server.PrintToChatAll($"{chatPrefix} Cannot add bots. Type .nobots and switch team, and then re-try .bot");
                    Server.PrintToChatAll($"{chatPrefix} 无法添加Bot. 先输入 .nobots 清除Bot，然后更换阵营，再输入.bot");
                }

                isSpawningBot = false;
            }
            catch (JsonException ex)
            {
                Log($"[SpawnBot - FATAL] Error: {ex.Message}");
            }
        }

        private CounterStrikeSharp.API.Modules.Timers.Timer? timer;
        public void TemporarilyDisableCollisions(CCSPlayerController p1, CCSPlayerController p2)
        {
            //if (p1 == null || p2 == null || p1.PlayerPawn == null || p2.PlayerPawn == null || p1.PlayerPawn.Value == null || p2.PlayerPawn.Value == null) return;

            // Reference collision code: https://github.com/Source2ZE/CS2Fixes/blob/f009e399ff23a81915e5a2b2afda20da2ba93ada/src/events.cpp#L150
            Log($"wobby state: {p1.PlayerPawn.Value.Collision.CollisionGroup}");
            p1.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DEBRIS;
            p1.PlayerPawn.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DEBRIS;
            p2.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DEBRIS;
            p2.PlayerPawn.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DEBRIS;
            // TODO: call CollisionRulesChanged
            var p1p = p1.PlayerPawn;
            var p2p = p2.PlayerPawn;
            timer?.Kill();
            timer = AddTimer(0.1f, () =>
            {
                if (!p1p.IsValid || !p2p.IsValid || !p1p.Value.IsValid || !p2p.Value.IsValid)
                {
                    Log($"player handle invalid p1p {p1p.Value.IsValid} p2p {p2p.Value.IsValid}");
                    timer?.Kill();
                    return;
                }
                if (!DoPlayersCollide(p1p.Value, p2p.Value))
                {
                    // Once they no longer collide 
                    p1p.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER_MOVEMENT;
                    p1p.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER_MOVEMENT;
                    p2p.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER_MOVEMENT;
                    p2p.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER_MOVEMENT;
                    // TODO: call CollisionRulesChanged
                    timer?.Kill();
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }

        public bool DoPlayersCollide(CCSPlayerPawn p1, CCSPlayerPawn p2)
        {
            Vector p1min, p1max, p2min, p2max;
            var p1pos = p1.AbsOrigin;
            var p2pos = p2.AbsOrigin;
            p1min = p1.Collision.Mins + p1pos!;
            p1max = p1.Collision.Maxs + p1pos!;
            p2min = p2.Collision.Mins + p2pos!;
            p2max = p2.Collision.Maxs + p2pos!;
            /* Log($"p1 ({p1min}, {p1max}), p2 ({p2min}, {p2max})"); */

            return p1min.X <= p2max.X && p1max.X >= p2min.X &&
                    p1min.Y <= p2max.Y && p1max.Y >= p2min.Y &&
                    p1min.Z <= p2max.Z && p1max.Z >= p2min.Z;
        }
        private static void ElevatePlayer(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null) return;
            player.PlayerPawn.Value.Teleport(new Vector(player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin.X, player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin.Y, player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin.Z + 80.0f), player.PlayerPawn.Value.EyeAngles, new Vector(0, 0, 0));
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (!IsPlayerValid(player)) return HookResult.Continue;

            // Respawing a bot where it was actually spawned during practice session
            Log($"Player {player.PlayerName} is spawned");
            if (isPractice && player!.IsValid && player.IsBot && player.UserId.HasValue)
            {
                Log($"2222222");
                if (pracUsedBots.ContainsKey(player.UserId.Value))
                {
                    Log($"3333333");
                    if (pracUsedBots[player.UserId.Value]["position"] is Position botPosition)
                    {
                        Log($"44444444");
                        player.PlayerPawn.Value?.Teleport(botPosition.PlayerPosition, botPosition.PlayerAngle, new Vector(0, 0, 0));
                        bool isCrouched = (bool)pracUsedBots[player.UserId.Value]["crouchstate"];
                        if (isCrouched)
                        {
                            player.PlayerPawn.Value!.Flags |= (uint)PlayerFlags.FL_DUCKING;
                            CCSPlayer_MovementServices movementService = new(player.PlayerPawn.Value.MovementServices!.Handle);
                            AddTimer(0.1f, () => movementService.DuckAmount = 1);
                            AddTimer(0.2f, () => player.PlayerPawn.Value.Bot!.IsCrouching = true);
                        }
                        CCSPlayerController? botOwner = (CCSPlayerController)pracUsedBots[player.UserId.Value]["owner"];
                        if (botOwner != null && botOwner.IsValid && botOwner.PlayerPawn != null && botOwner.PlayerPawn.IsValid)
                        {
                            AddTimer(0.2f, () => TemporarilyDisableCollisions(botOwner, player));
                        }
                    }
                }
                else if (!isSpawningBot && !player.IsHLTV)
                {
                    // Bot has been spawned, but we didn't spawn it, so kick it.
                    // This most often happens when a player changes team with bot_quota_mode set to fill
                    // Extra bots from bot_add are already handled in SpawnBot
                    // Delay this for a few seconds to prevent crashes
                    Log($"Kicking bot {player.PlayerName} due to erroneous spawning");
                    AddTimer(2.5f, () =>
                    {
                        Server.ExecuteCommand($"bot_kick {player.PlayerName}");
                    });
                }
            }

            return HookResult.Continue;
        }

        [ConsoleCommand("css_nobots", "Removes bots from the practice session")]
        public void OnNoBotsCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;
            Server.ExecuteCommand("bot_kick");
            pracUsedBots = new Dictionary<int, Dictionary<string, object>>();
        }

        [ConsoleCommand("css_ff", "Fast forwards the timescale to 20 seconds")]
        public void OnFFCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;

            Dictionary<int, MoveType_t> preFastForwardMoveTypes = new();

            foreach (var key in playerData.Keys) {
                preFastForwardMoveTypes[key] = playerData[key].PlayerPawn.Value.MoveType;
                playerData[key].PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
            }

            Server.PrintToChatAll($"{chatPrefix} Fastforwarding 20 seconds!");
            Server.ExecuteCommand("host_timescale 10");
            AddTimer(20.0f, () => {
                ResetFastForward(preFastForwardMoveTypes);
            });

        }

        [ConsoleCommand("css_fastforward", "Fast forwards the timescale to 20 seconds")]
        public void OnFastForwardCommand(CCSPlayerController? player, CommandInfo? command)
        {
            OnFFCommand(player, command);
        }

        public void ResetFastForward(Dictionary<int, MoveType_t> preFastForwardMoveTypes) {
            if (!isPractice) return;
            Server.ExecuteCommand("host_timescale 1");
            foreach (var key in playerData.Keys) {
                playerData[key].PlayerPawn.Value.MoveType = preFastForwardMoveTypes[key];
            }
        }
        public void KillFlashEffect(CCSPlayerController player)
        {
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null) return;
            Log($"[KillFlashEffect] Killing flash effect for player: {player.PlayerName}");
            playerPawn.FlashMaxAlpha = 0.5f;
        }

        [ConsoleCommand("css_clear", "Removes all the available granades")]
        public void OnClearCommand(CCSPlayerController? player, CommandInfo? command)
        {
            RemoveGrenadeEntities();
        }

        public void RemoveGrenadeEntities()
        {
            if (!isPractice) return;
            var smokes = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            foreach (var entity in smokes)
            {
                entity?.Remove();
            }
            var mollys = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("molotov_projectile");
            foreach (var entity in mollys)
            {
                entity?.Remove();
            }
            var inferno = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("inferno");
            foreach (var entity in inferno)
            {
                entity?.Remove();
            }
        }

        public void ExecUnpracCommands() {
            Server.ExecuteCommand("sv_cheats false;sv_grenade_trajectory_prac_pipreview false;sv_grenade_trajectory_prac_trailtime 0; mp_ct_default_grenades \"\"; mp_ct_default_primary \"\"; mp_t_default_grenades\"\"; mp_t_default_primary\"\"; mp_teammates_are_enemies false;");
            Server.ExecuteCommand("mp_death_drop_breachcharge true; mp_death_drop_defuser true; mp_death_drop_taser true; mp_drop_knife_enable false; mp_death_drop_grenade 2; ammo_grenade_limit_total 4; mp_defuser_allocation 0; sv_infinite_ammo 0; mp_force_pick_time 15");
        }

        public bool IsValidPositionForLastGrenade(CCSPlayerController player, int position)
        {
            int userId = player.UserId!.Value;
            if (!lastGrenadesData.ContainsKey(userId) || lastGrenadesData[userId].Count <= 0)
            {
                PrintToPlayerChat(player, $"You have not thrown any nade yet!");
                return false;
            }

            if (lastGrenadesData[userId].Count < position)
            {
                PrintToPlayerChat(player, $"Your grenade history only goes from 1 to {lastGrenadesData[userId].Count}!");
                return false;
            }

            return true;
        }

        public void RethrowSpecificNade(CCSPlayerController player, string nadeType)
        {
            if (!isPractice || !player.UserId.HasValue) return;
            int userId = player.UserId.Value;
            if (!nadeSpecificLastGrenadeData.ContainsKey(userId) || !nadeSpecificLastGrenadeData[userId].ContainsKey(nadeType))
            {
                PrintToPlayerChat(player, $"You have not thrown any {nadeType} yet!");
                return;
            }
            GrenadeThrownData grenadeThrown = nadeSpecificLastGrenadeData[userId][nadeType];
            if (grenadeThrown != null) AddTimer(grenadeThrown.Delay, () => grenadeThrown.Throw(player));
        }

        public void HandleBackCommand(CCSPlayerController player, string number)
        {
            if (!isPractice || player == null || !player.UserId.HasValue) return;
            int userId = player.UserId.Value;
            if (!string.IsNullOrWhiteSpace(number))
            {
                if (int.TryParse(number, out int positionNumber) && positionNumber >= 1)
                {
                    if (IsValidPositionForLastGrenade(player, positionNumber))
                    {
                        positionNumber -= 1;
                        lastGrenadesData[userId][positionNumber].LoadPosition(player);
                        PrintToPlayerChat(player, $"Teleported to grenade of history position: {positionNumber+1}/{lastGrenadesData[userId].Count}");
                    }
                }
                else
                {
                    PrintToPlayerChat(player, $"Invalid value for !back command. Please specify a valid non-negative number. Usage: !back <number>");
                    return;
                }
            }
            else
            {
                int thrownCount = lastGrenadesData.ContainsKey(userId) ? lastGrenadesData[userId].Count : 0;
                ReplyToUserCommand(player, $"Usage: !back <number> (You've thrown {thrownCount} grenades till now)");
            }
        }

        public void HandleThrowIndexCommand(CCSPlayerController player, string argString)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            int userId = player!.UserId!.Value;

            string[] argsList = argString.Split();

            foreach (string arg in argsList)
            {
                if (int.TryParse(arg, out int positionNumber) && positionNumber >= 1)
                {
                    if (IsValidPositionForLastGrenade(player, positionNumber))
                    {
                        positionNumber -= 1;
                        GrenadeThrownData grenadeThrown = lastGrenadesData[userId][positionNumber];
                        if (grenadeThrown != null) AddTimer(grenadeThrown.Delay, () => grenadeThrown.Throw(player));
                        PrintToPlayerChat(player, $"Throwing grenade of history position: {positionNumber+1}/{lastGrenadesData[userId].Count}");
                    }
                }
                else
                {
                    PrintToPlayerChat(player, $"'{arg}' is not a valid non-negative number for !throwindex command.");
                }
            }
        }

        public void HandleDelayCommand(CCSPlayerController player, string delay)
        {
            if (!isPractice || !IsPlayerValid(player)) return;

            if (!isPractice || player == null || !player.UserId.HasValue) return;
            int userId = player.UserId.Value;
            if (string.IsNullOrWhiteSpace(delay))
            {
                ReplyToUserCommand(player, $"Usage: !delay <delay_in_seconds>");
                return;
            }

            if (float.TryParse(delay, out float delayInSeconds) && delayInSeconds > 0)
            {
                if (IsValidPositionForLastGrenade(player, 0))
                {
                    lastGrenadesData[userId].Last().Delay = delayInSeconds;
                    PrintToPlayerChat(player, $"Delay of {delayInSeconds:0.00}s set for grenade of index: {lastGrenadesData[userId].Count}.");
                }
            }
            else
            {
                PrintToPlayerChat(player, $"Delay should be valid float number and greater than 0 seconds.");
                return;
            }
        }

        public void DisplayPracticeTimerCenter(int userId)
        {
            if (!playerData.ContainsKey(userId) || !playerTimers.ContainsKey(userId)) return;
            if (!IsPlayerValid(playerData[userId])) return;
            playerTimers[userId].DisplayTimerCenter(playerData[userId]);
        }

        [ConsoleCommand("css_throw", "Throws the last thrown grenade")]
        [ConsoleCommand("css_rethrow", "Throws the last thrown grenade")]
        public void OnRethrowCommand(CCSPlayerController? player, CommandInfo? command)
        {

            if (!isPractice || player == null || !player.UserId.HasValue) return;
            int userId = player.UserId.Value;
            if (!lastGrenadesData.ContainsKey(userId) || lastGrenadesData[userId].Count <= 0)
            {
                PrintToPlayerChat(player, $"You have not thrown any nade yet!");
                return;
            }
            GrenadeThrownData lastGrenade = lastGrenadesData[userId].Last();
            if (lastGrenade != null) AddTimer(lastGrenade.Delay, () => lastGrenade.Throw(player));
        }

        [ConsoleCommand("css_throwsmoke", "Throws the last thrown smoke")]
        [ConsoleCommand("css_rethrowsmoke", "Throws the last thrown smoke")]
        public void OnRethrowSmokeCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            RethrowSpecificNade(player, "smoke");
        }

        [ConsoleCommand("css_throwflash", "Throws the last thrown flash")]
        [ConsoleCommand("css_rethrowflash", "Throws the last thrown flash")]
        public void OnRethrowFlashCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            RethrowSpecificNade(player, "flash");
        }

        [ConsoleCommand("css_throwgrenade", "Throws the last thrown he grenade")]
        [ConsoleCommand("css_rethrowgrenade", "Throws the last thrown he grenade")]
        [ConsoleCommand("css_thrownade", "Throws the last thrown he grenade")]
        [ConsoleCommand("css_rethrownade", "Throws the last thrown he grenade")]
        public void OnRethrowGrenadeCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            RethrowSpecificNade(player, "hegrenade");
        }

        [ConsoleCommand("css_throwmolotov", "Throws the last thrown molotov")]
        [ConsoleCommand("css_rethrowmolotov", "Throws the last thrown molotov")]
        public void OnRethrowMolotovCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            RethrowSpecificNade(player, "molotov");
        }

        [ConsoleCommand("css_throwdecoy", "Throws the last thrown decoy")]
        [ConsoleCommand("css_rethrowdecoy", "Throws the last thrown decoy")]
        public void OnRethrowDecoyCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            RethrowSpecificNade(player, "decoy");
        }

        [ConsoleCommand("css_last", "Teleports to the last thrown grenade position")]
        public void OnLastCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null || !player.UserId.HasValue) return;
            int userId = player.UserId.Value;
            if (!lastGrenadesData.ContainsKey(userId) || lastGrenadesData[userId].Count <= 0)
            {
                PrintToPlayerChat(player, $"You have not thrown any nade yet!");
                return;
            }
            lastGrenadesData[userId].Last().LoadPosition(player);
        }

        [ConsoleCommand("css_back", "Teleports to the provided position in grenade thrown history")]
        public void OnBackCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice || player == null || !player.UserId.HasValue) return;
            if (command.ArgCount >= 2) 
            {
                string commandArg = command.ArgByIndex(1);
                HandleBackCommand(player, commandArg);
            }
            else 
            {
                int userId = player!.UserId!.Value;
                int thrownCount = lastGrenadesData.ContainsKey(userId) ? lastGrenadesData[userId].Count : 0;
                ReplyToUserCommand(player, $"Usage: !back <number> (You've thrown {thrownCount} grenades till now)");
            }      
        }

        [ConsoleCommand("css_throwidx", "Throws grenade of provided position in grenade thrown history")]
        [ConsoleCommand("css_throwindex", "Throws grenade of provided position in grenade thrown history")]
        public void OnThrowIndexCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            if (command.ArgCount >= 2) 
            {
                HandleThrowIndexCommand(player!, command.ArgString);
            }
            else 
            {
                int userId = player!.UserId!.Value;
                int thrownCount = lastGrenadesData.ContainsKey(userId) ? lastGrenadesData[userId].Count : 0;
                ReplyToUserCommand(player, $"Usage: !throwindex <number> (You've thrown {thrownCount} grenades till now)");
            }      
        }

        [ConsoleCommand("css_lastindex", "Returns index of the last thrown grenade")]
        public void OnLastIndexCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            if (IsValidPositionForLastGrenade(player!, 1))
            {
                PrintToPlayerChat(player!, $"Index of last thrown grenade: {lastGrenadesData[player!.UserId!.Value].Count}");
            } 
        }

        [ConsoleCommand("css_delay", "Adds a delay to the last thrown grenade. Usage: !delay <delay_in_seconds>")]
        public void OnDelayCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            if (command.ArgCount >= 2) 
            {
                HandleDelayCommand(player!, command.ArgByIndex(1));
            }
            else 
            {
                ReplyToUserCommand(player, $"Usage: !delay <delay_in_seconds>");
            }      
        }

        [ConsoleCommand("css_timer", "Starts a timer, use .timer again to stop it.")]
        public void OnTimerCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice || !IsPlayerValid(player)) return;
            int userId = player!.UserId!.Value;
            if (playerTimers.ContainsKey(userId))
            {
                playerTimers[userId].KillTimer();
                double timerResult = playerTimers[userId].GetTimerResult();
                player.PrintToCenter($"Timer: {timerResult}s");
                PrintToPlayerChat(player, $"Timer stopped! Result: {timerResult}s");
                playerTimers.Remove(userId);
            }
            else
            {
                playerTimers[userId] = new PlayerPracticeTimer(PracticeTimerType.OnMovement)
                {
                    StartTime = DateTime.Now,
                    Timer = AddTimer(0.1f, () => DisplayPracticeTimerCenter(userId), TimerFlags.REPEAT)
                };
                PrintToPlayerChat(player, $"Timer started! User !timer to stop it.");
            }
        }

        // Todo: Implement timer2 when we have OnPlayerRunCmd in CS#. Using OnTick would be its alternative, it would be very expensive and not worth it.
        // [ConsoleCommand("css_timer2", "Starts a timer, use .timer2 again to stop it.")]
        // public void OnTimer2Command(CCSPlayerController? player, CommandInfo command)
        // {
        //     if (!isPractice || !IsPlayerValid(player)) return;
        //     int userId = player!.UserId!.Value;
        //     if (playerTimers.ContainsKey(userId))
        //     {
        //         PrintToPlayerChat(player, $"Timer stopped! Result: {playerTimers[userId].GetTimerResult()}s");
        //         playerTimers[userId].KillTimer();
        //         playerTimers.Remove(userId);
        //     }
        //     else
        //     {
        //         playerTimers[userId] = new PlayerPracticeTimer(PracticeTimerType.OnMovement);
        //         PrintToPlayerChat(player, $"When you start moving a timer will run until you stop moving.");
        //     }
        // }
    }
}
