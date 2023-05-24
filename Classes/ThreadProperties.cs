using BeatenGamesService.Services;
using GameListDB.Model;
using Google.Protobuf.Collections;

namespace BeatenGamesService
{
    public class ThreadProperties
    {
        public ThreadProperties(ref RepeatedField<BacklogItem> output,GameListDB.Model.Backlog game) 
        {
            this.output = output;
            this.game = game;
        }

        public RepeatedField<BacklogItem> output;

        public GameListDB.Model.Backlog game;

    }
}