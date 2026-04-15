using BreathCasino.Core;

namespace BreathCasino.Gameplay
{
    public readonly struct HudSnapshot
    {
        public readonly GameMode Mode;
        public readonly GamePhase Phase;
        public readonly int RoundNumber;
        public readonly int RoundCount;
        public readonly int MiniRound;
        public readonly int PlayerHp;
        public readonly int PlayerMaxHp;
        public readonly int EnemyHp;
        public readonly int EnemyMaxHp;
        public readonly float OxygenSeconds;
        public readonly string ChamberDescription;
        public readonly string PlayerTickets;
        public readonly string EnemyTickets;
        public readonly string LastEvent;

        public HudSnapshot(
            GameMode mode,
            GamePhase phase,
            int roundNumber,
            int roundCount,
            int miniRound,
            int playerHp,
            int playerMaxHp,
            int enemyHp,
            int enemyMaxHp,
            float oxygenSeconds,
            string chamberDescription,
            string playerTickets,
            string enemyTickets,
            string lastEvent)
        {
            Mode = mode;
            Phase = phase;
            RoundNumber = roundNumber;
            RoundCount = roundCount;
            MiniRound = miniRound;
            PlayerHp = playerHp;
            PlayerMaxHp = playerMaxHp;
            EnemyHp = enemyHp;
            EnemyMaxHp = enemyMaxHp;
            OxygenSeconds = oxygenSeconds;
            ChamberDescription = chamberDescription;
            PlayerTickets = playerTickets;
            EnemyTickets = enemyTickets;
            LastEvent = lastEvent;
        }
    }
}
