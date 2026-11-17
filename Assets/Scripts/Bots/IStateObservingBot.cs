// Observation adapters must retain only owned/public information or sanitized search worlds.
public interface IStateObservingBot
{
    void Observe(GameState state, int player);
}
