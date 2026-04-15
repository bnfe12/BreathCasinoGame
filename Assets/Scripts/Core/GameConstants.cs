namespace BreathCasino.Core
{
    /// <summary>
    /// Константы для игровой логики Breath Casino.
    /// </summary>
    public static class GameConstants
    {
        // ═══ TIMING ═══
        public const float PHASE_DELAY = 0.8f;
        public const float ROUND_TRANSITION_DELAY = 1.25f;
        public const float DUEL_RESULT_VIEW_TIME = 2.5f;
        public const float CARD_MOVE_DURATION = 0.22f;
        
        // ═══ OXYGEN ═══
        public const float O2_BONUS_MAIN_CARD = 10f;
        public const float O2_BONUS_SPECIAL_CARD = 10f;
        public const float O2_CRITICAL_THRESHOLD = 20f;
        public const float LAST_BREATH_DURATION = 15f;
        
        // ═══ SHOOTING ═══
        public const float EXPLOSIVE_CHANCE = 0.2f;
        public const int DAMAGE_LIVE = 1;
        public const int DAMAGE_EXPLOSIVE = 2;
        
        // ═══ AI ═══
        public const float AI_BLANK_THRESHOLD = 0.6f;
        
        // ═══ TICKETS ═══
        public const int LIFE_FOR_CARDS_AMOUNT = 6;
        
        // ═══ CARD GENERATION ═══
        public const float THREAT_CHANCE = 0.35f;
        public const int MIN_CARD_WEIGHT = 2;
        public const int MAX_CARD_WEIGHT = 8;
        public const int MAX_RESOURCE_WEIGHT = 7;
        
        // ═══ DECK SIZES ═══
        public static readonly int[] DECK_SIZES_PER_ROUND = { 12, 17, 24 };
        public static readonly int[] HAND_SIZES_PER_ROUND = { 4, 5, 6 };
        
        // ═══ EMERGENCY ═══
        public const int EMERGENCY_DECK_SIZE = 6;
        public const int EMERGENCY_HAND_SIZE = 2;
    }
}
