using _.Scripts.Gameplay;
using _.Scripts.Utility.Structures;

namespace _.Scripts.Services
{
    public class GameplayService
    {
        private GameState _gameState;
        private Session   _session;

        public GameState GameState => _gameState;
        public Session Session => _session;

        public GameplayService()
        {
            _gameState = new GameState();
        }

        public void StartSession(Session session)
        {
            _session = session;
        }
    }
}