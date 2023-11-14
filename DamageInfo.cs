using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{

    public partial class MatchZy
    {

		public Dictionary<int, Dictionary<int, DamagePlayerInfo>> playerDamageInfo = new Dictionary<int, Dictionary<int, DamagePlayerInfo>>();
		private void UpdatePlayerDamageInfo(EventPlayerHurt @event, int targetId)
		{
			int attackerId = (int)@event.Attacker.UserId!;
			if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
				playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

			if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
				attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();

			targetInfo.DamageHP += @event.DmgHealth;
			targetInfo.Hits++;
		}

        private void ShowDamageInfo()
		{
			HashSet<(int, int)> processedPairs = new HashSet<(int, int)>();

			foreach (var entry in playerDamageInfo)
			{
				int attackerId = entry.Key;
				foreach (var (targetId, targetEntry) in entry.Value)
				{
					if (processedPairs.Contains((attackerId, targetId)) || processedPairs.Contains((targetId, attackerId)))
						continue;

					// Access and use the damage information as needed.
					int damageGiven = targetEntry.DamageHP;
					int hitsGiven = targetEntry.Hits;
					int damageTaken = 0;
					int hitsTaken = 0;

					if (playerDamageInfo.TryGetValue(targetId, out var targetInfo) && targetInfo.TryGetValue(attackerId, out var takenInfo))
					{
						damageTaken = takenInfo.DamageHP;
						hitsTaken = takenInfo.Hits;
					}

					var attackerController = Utilities.GetPlayerFromUserid(attackerId);
					var targetController = Utilities.GetPlayerFromUserid(targetId);

					if (attackerController != null && targetController != null)
					{
						int attackerHP = attackerController.PlayerPawn.Value.Health < 0 ? 0 : attackerController.PlayerPawn.Value.Health;
						string attackerName = attackerController.PlayerName;

						int targetHP = targetController.PlayerPawn.Value.Health < 0 ? 0 : targetController.PlayerPawn.Value.Health;
						string targetName = targetController.PlayerName;

						attackerController.PrintToChat($"{chatPrefix} {ChatColors.Green}To: [{damageGiven} / {hitsGiven} hits] From: [{damageTaken} / {hitsTaken} hits] - {targetName} - ({targetHP} hp){ChatColors.Default}");
						targetController.PrintToChat($"{chatPrefix} {ChatColors.Green}To: [{damageTaken} / {hitsTaken} hits] From: [{damageGiven} / {hitsGiven} hits] - {attackerName} - ({attackerHP} hp){ChatColors.Default}");
					}

					// Mark this pair as processed to avoid duplicates.
					processedPairs.Add((attackerId, targetId));
				}
			}
			playerDamageInfo.Clear();
		}
    }

	public class DamagePlayerInfo
	{
		public int DamageHP { get; set; } = 0;
		public int Hits { get; set; } = 0;
	}
}