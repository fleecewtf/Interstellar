using GorillaGameModes;
using GorillaTagScripts;

namespace Interstellar.Utilities
{
    public static class GameModeUtilities
    {
        public static void AddInfected(NetPlayer plr)
        {
            if (!NetworkSystem.Instance.InRoom || GorillaGameManager.instance == null || plr == null)
                return;

            switch (GorillaGameManager.instance.GameType())
            {
                case GameModeType.Infection:
                case GameModeType.InfectionCompetitive:
                case GameModeType.SuperInfect:
                case GameModeType.FreezeTag:
                case GameModeType.PropHunt:
                    if (GorillaGameManager.instance is GorillaTagManager tagManager)
                    {
                        if (tagManager.isCurrentlyTag)
                            tagManager.ChangeCurrentIt(plr);
                        else if (tagManager.currentInfected != null && !tagManager.currentInfected.Contains(plr))
                            tagManager.AddInfectedPlayer(plr);
                    }
                    break;

                case GameModeType.Ghost:
                case GameModeType.Ambush:
                    if (GorillaGameManager.instance is GorillaAmbushManager ghostManager)
                    {
                        if (ghostManager.isCurrentlyTag)
                            ghostManager.ChangeCurrentIt(plr);
                        else if (ghostManager.currentInfected != null && !ghostManager.currentInfected.Contains(plr))
                            ghostManager.AddInfectedPlayer(plr);
                    }
                    break;

                case GameModeType.Paintbrawl:
                    if (GorillaGameManager.instance is GorillaPaintbrawlManager paintbrawlManager && paintbrawlManager.playerLives != null)
                        paintbrawlManager.playerLives[plr.ActorNumber] = 0;

                    break;
            }
        }

    }
}
