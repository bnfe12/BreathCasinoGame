using System;

namespace BreathCasino.Core
{
    public enum GamePhase
    {
        Waiting,
        BulletReveal,
        Dealing,
        Attack,
        Defense,
        Resolution,
        Shooting,
        RoundOver,
        GameOver
    }

    public enum Side
    {
        Player,
        Enemy
    }

    public enum BulletType
    {
        Blank,
        Live,
        Explosive
    }

    public enum TicketType
    {
        Inspection,
        DoubleDamage,
        MedicalRation,
        VoidTransaction,
        Shuffle,
        LifeForCards
    }

    public enum CardType
    {
        Resource,
        Threat,
        Special
    }

    public enum GameMode
    {
        Spectator,
        Player
    }

    public enum SpecialEffectType
    {
        None,
        Cancel,
        Steal,
        Duplicate,
        Exchange,
        Block,
        Prohibit
    }

    public enum SlotKind
    {
        Slot1,
        Slot2
    }

    [Serializable]
    public struct RoundConfig
    {
        public int playerHp;
        public int enemyHp;
        public float oxygenSeconds;
        public int minBullets;
        public int maxBullets;
        public bool allowExplosive;
    }

    [Serializable]
    public struct CardData
    {
        public string cardName;
        public int weight;
        public CardType cardType;
        public SpecialEffectType specialEffect;

        public bool IsThreat => cardType == CardType.Threat;
        public bool IsResource => cardType == CardType.Resource;
    }
}