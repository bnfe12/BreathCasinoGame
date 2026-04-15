using System.Text;

namespace BreathCasino.Gameplay
{
    public static class HudFormatter
    {
        public static string Build(HudSnapshot snapshot, bool hasValidationErrors)
        {
            StringBuilder builder = new();
            builder.AppendLine(BCLocalization.Get("hud.title"));
            builder.AppendLine($"{BCLocalization.Get("hud.mode")}: {snapshot.Mode} | {BCLocalization.Get("hud.phase")}: {snapshot.Phase}");
            builder.AppendLine($"{BCLocalization.Get("hud.round")}: {snapshot.RoundNumber} / {snapshot.RoundCount}");
            builder.AppendLine($"{BCLocalization.Get("hud.mini_round")}: {snapshot.MiniRound}");
            builder.AppendLine($"{BCLocalization.Get("hud.player_hp")}: {snapshot.PlayerHp}/{snapshot.PlayerMaxHp}");
            builder.AppendLine($"{BCLocalization.Get("hud.enemy_hp")}: {snapshot.EnemyHp}/{snapshot.EnemyMaxHp}");
            builder.AppendLine($"{BCLocalization.Get("hud.oxygen")}: {snapshot.OxygenSeconds:0.0}s");
            builder.AppendLine($"{BCLocalization.Get("hud.chamber")}: {snapshot.ChamberDescription}");
            builder.AppendLine($"{BCLocalization.Get("hud.player_tickets")}: {snapshot.PlayerTickets}");
            builder.AppendLine($"{BCLocalization.Get("hud.enemy_tickets")}: {snapshot.EnemyTickets}");
            builder.AppendLine($"{BCLocalization.Get("hud.last_event")}: {BCLocalization.LocalizeRuntimeText(snapshot.LastEvent)}");

            if (hasValidationErrors)
            {
                builder.AppendLine(BCLocalization.Get("hud.validation"));
            }

            return builder.ToString();
        }
    }
}
