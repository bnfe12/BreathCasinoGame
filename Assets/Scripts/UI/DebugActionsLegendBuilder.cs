using System.Text;

namespace BreathCasino.Gameplay
{
    public static class DebugActionsLegendBuilder
    {
        public static string Build()
        {
            StringBuilder builder = new();
            builder.AppendLine(BCLocalization.Get("debug.title"));
            builder.AppendLine(BCLocalization.Get("debug.toggle_help"));
            builder.AppendLine(BCLocalization.Get("debug.restart"));
            builder.AppendLine(BCLocalization.Get("debug.round2"));
            builder.AppendLine(BCLocalization.Get("debug.round3"));
            builder.AppendLine(BCLocalization.Get("debug.ticket_player"));
            builder.AppendLine(BCLocalization.Get("debug.ticket_enemy"));
            builder.AppendLine(BCLocalization.Get("debug.mechanism"));
            builder.AppendLine(BCLocalization.Get("debug.ticket_player_toggle"));
            builder.AppendLine(BCLocalization.Get("debug.ticket_enemy_toggle"));
            builder.AppendLine(BCLocalization.Get("debug.dialogue"));
            builder.AppendLine(BCLocalization.Get("debug.damage"));
            builder.AppendLine(BCLocalization.Get("debug.gun"));
            builder.AppendLine(BCLocalization.Get("debug.pickup"));
            return builder.ToString();
        }
    }
}
