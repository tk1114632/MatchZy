using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{
    public class coachPlayer
    {
        public CCSPlayerController player { get; set; }
        public CsTeam team { get; set; }

        public coachPlayer(CCSPlayerController player, CsTeam team) 
        {  
            this.player = player;
            this.team = team;
        }
    }

    public class Team 
    {
        public required string teamName;
        public string teamFlag = "";
        public string teamTag = "";

        public List<CCSPlayerController> teamPlayers = new List<CCSPlayerController>();

        public CCSPlayerController? coach;
    }

    public partial class MatchZy
    {
        //define coachList
        public List<coachPlayer> coachPlayers = new List<coachPlayer>();

        [ConsoleCommand("css_coach", "Sets coach for the requested team")]
        public void OnCoachCommand(CCSPlayerController? player, CommandInfo? command) 
        {
            Log($"[OnCoachCommand]");
            HandleCoachCommand(player, command.ArgString);
        }

        [ConsoleCommand("css_uncoach", "Sets coach for the requested team")]
        public void OnUnCoachCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null || !player.PlayerPawn.IsValid) return;
            if (isPractice) {
                ReplyToUserCommand(player, "Uncoach is only available in match mode!");
                ReplyToUserCommand(player, ".uncoach 只能在比赛模式使用");
                return;
            }

            /*Team matchZyCoachTeam;

            if (matchzyTeam1.coach == player) {
                matchZyCoachTeam = matchzyTeam1;
            }
            else if (matchzyTeam2.coach == player) {
                matchZyCoachTeam = matchzyTeam2;
            }
            else {
                ReplyToUserCommand(player, "You are not coaching any team!");
                ReplyToUserCommand(player, "你不在教练位");
                return;
            }*/

            if(coachPlayers.Find(x=>x.player == player) == null)
            {
                ReplyToUserCommand(player, "You are not coaching any team!");
                ReplyToUserCommand(player, "你不在教练位");
                return;
            }
            else
            {
                player.Clan = "";
                if (player.InGameMoneyServices != null) player.InGameMoneyServices.Account = 0;
                ReplyToUserCommand(player, "You are now not coaching team anymore!");
                ReplyToUserCommand(player, "你已退出教练位");
                coachPlayers.Remove(coachPlayers.Find(x => x.player == player));
            }
            
        }

        public void HandleCoachCommand(CCSPlayerController player, string side) {
            if (player == null || !player.PlayerPawn.IsValid) return;
            if (isPractice) {
                ReplyToUserCommand(player, "Coach is only available in warmup period of match mode!");
                ReplyToUserCommand(player, ".coach 只能在比赛模式的热身阶段加入教练位");
                return;
            }

            side = side.Trim().ToLower();

            if (side != "t" && side != "ct") {
                ReplyToUserCommand(player, "Usage: .coach t or .coach ct");
                ReplyToUserCommand(player, "用法: .coach t 或 .coach ct");
                return;
            }

            if (matchzyTeam1.coach == player || matchzyTeam2.coach == player) 
            {
                ReplyToUserCommand(player, "You are already coaching a team!");
                ReplyToUserCommand(player, "你已经在教练位了");
                return;
            }

            /*Team matchZyCoachTeam;

            if (side == "t") {
                matchZyCoachTeam = reverseTeamSides["TERRORIST"];
            } else if (side == "ct") {
                matchZyCoachTeam = reverseTeamSides["CT"];
            } else {
                return;
            }

            *//*if (matchZyCoachTeam.coach != null) {
                ReplyToUserCommand(player, "Only 1 coach slot available!");
                ReplyToUserCommand(player, "只有1个教练位可供使用");
                return;
            }*//*

            matchZyCoachTeam.coach = player;*/

            coachPlayer theCoach = new coachPlayer(player, CsTeam.Spectator);
            if (side == "t")
            {
                theCoach.team = CsTeam.Terrorist;
                Server.PrintToChatAll($"{chatPrefix} {ChatColors.Default}{player.PlayerName} is now coaching {ChatColors.Green}Team T{ChatColors.Default}!");
            }
            else if (side == "ct")
            {
                theCoach.team = CsTeam.CounterTerrorist;
                Server.PrintToChatAll($"{chatPrefix} {ChatColors.Default}{player.PlayerName} is now coaching {ChatColors.Green}Team CT{ChatColors.Default}!");
            }
            else
            {
                return;
            }
            if(coachPlayers.Find(x => x.player == player) == null)
            {
                coachPlayers.Add(theCoach);
                Log("The player is added to coach list");
            }
            else
            {
                coachPlayers.Remove(coachPlayers.Find(x => x.player == player));
                coachPlayers.Add(theCoach);
                Log("The player is already on list, remove old and add new");
            }
            
            player.Clan = $"[ COACH ]";
            if (player.InGameMoneyServices != null) player.InGameMoneyServices.Account = 0;

            if(isMatchLive)
            {
                player.ChangeTeam(theCoach.team);
            }
            
            /*ReplyToUserCommand(player, $"Now coaching {matchZyCoachTeam.teamName}! Use .uncoach to stop coaching");
            ReplyToUserCommand(player, $"进入教练位: {matchZyCoachTeam.teamName}! 输入 .uncoach 退出教练位");
            Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{player.PlayerName}{ChatColors.Default} is now coaching {ChatColors.Green}{matchZyCoachTeam.teamName}{ChatColors.Default}!");
            Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{player.PlayerName}{ChatColors.Default} 现在是 {ChatColors.Green}{matchZyCoachTeam.teamName}{ChatColors.Default} 的教练");*/
        }

        public void HandleCoaches() 
        {
            /*List<CCSPlayerController?> coaches = new List<CCSPlayerController?>
            {
                matchzyTeam1.coach,
                matchzyTeam2.coach
            };*/

            /*foreach (var coach in coaches) 
            {
                if (coach == null) continue;
                Log($"Found coach: {coach.PlayerName}");
                coach.InGameMoneyServices!.Account = 0;
                AddTimer(0.5f, () => HandleCoachTeam(coach, true));
                coach.ActionTrackingServices!.MatchStats.Kills = 0;
                coach.ActionTrackingServices!.MatchStats.Deaths = 0;
                coach.ActionTrackingServices!.MatchStats.Assists = 0;
                coach.ActionTrackingServices!.MatchStats.Damage = 0;

                MoveCoach(coach);
                RemoveCoachWeaponsAndDropC4(coach);
            }*/
            bool TCoachExists = false;
            bool C4FoundOnCoach = false;
            foreach (var coach in coachPlayers)
            {
                if (coach == null || !coach.player.IsValid) continue;
                //Log($"Found coach: {coach.player.PlayerName}");
                if(coach.team == CsTeam.Terrorist) {
                    TCoachExists = true;
                }
                coach.player.InGameMoneyServices!.Account = 0;
                AddTimer(0.5f, () => HandleCoachTeamNew(coach.player, coach.team, true));
                coach.player.ActionTrackingServices!.MatchStats.Kills = 0;
                coach.player.ActionTrackingServices!.MatchStats.Deaths = 0;
                coach.player.ActionTrackingServices!.MatchStats.Assists = 0;
                coach.player.ActionTrackingServices!.MatchStats.Damage = 0;

                MoveCoachNew(coach.player, coach.team);
                
                try
                {
                    C4FoundOnCoach |= RemoveCoachWeaponsAndDropC4(coach.player);
                }
                catch (Exception ex)
                {
                    Log("Handle coaches in coachplayers loop e:" + ex.Message);
                }     
                
            }
            if(TCoachExists && C4FoundOnCoach)
            {
                GiveC4ToARandomPlayer();
            }
        }
        // Todo: Organize Teams code which can be later used for setting up matches
    }
}
