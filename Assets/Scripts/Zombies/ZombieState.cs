namespace ZombieAI
{
    /// <summary>
    /// All possible states in the Zombie finite state machine.
    /// </summary>
    public enum ZombieState
    {
        Idle,
        InvestigateSound,
        Chase,
        SearchArea,
        Attack
    }
}
