namespace TreeFrogAdventure.Fx
{
    /// <summary>
    /// The moments in the game that deserve a puff of something.
    ///
    /// Kept as an enum rather than string keys so a typo is a compile error and the
    /// validator can assert the catalog covers every value.
    /// </summary>
    public enum FxKind
    {
        EnemyHit,
        EnemyDeath,
        Collect,
        Land,
        Splash,
        BossPhase,
        Checkpoint,
    }
}
