using Grpc.Core;
using System.Text.Json;
using BeatenGamesService;
using GameListDB.Model;
using GameListDB.Model.Extensions;
using Google.Protobuf.Collections;


namespace BeatenGamesService.Services;

public class BacklogListService : Backlog.BacklogBase
{
    private const int MillisecondsTimeout = 100;
    private const int MaxThread = 3;

    private readonly ILogger<BacklogListService> _logger;
    private readonly GameListDB.IGDBIntegration.IGDBQueryBase igdb;
    private readonly IServiceProvider serviceProvider;

    public BacklogListService(ILogger<BacklogListService> logger,
    IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        _logger = logger;
        var config = GameListDB.ConfigUtils.ReadConfig(true);
        igdb = new GameListDB.IGDBIntegration.IGDBQueryBase(new IGDB.IGDBClient(config.IGDB_CLIENT_ID, config.IGDB_CLIENT_SECRET));
    }

    /// Endpoints

    public override Task<BacklogReply> RequestBacklog(BacklogRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BacklogReply
        {
            BacklogItems =
            {
                GetGRPCGameList
                (
                    serviceProvider
                    .GetRequiredService<GameListsContext>()
                    .GetBeatenGames(request.Year)
                )
            },
        });
    }

    public override Task<BacklogReply> GetTopBacklogGames(TopRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BacklogReply
        {
            BacklogItems =
            {
                GetGRPCGameList
                (
                    serviceProvider
                    .GetRequiredService<GameListsContext>()
                    .GetUnbeatenTopScoredGames(request.Index,request.Length)
                )
            },
        });
    }

    public override Task<BacklogReply> GetTopPriorizedBacklogGames(TopRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BacklogReply
        {
            BacklogItems =
            {
                GetGRPCGameList
                (
                    serviceProvider
                    .GetRequiredService<GameListsContext>()
                    .GetUnbeatenTop10PrioritesGames(request.Index,request.Length)
                )
            },
        });
    }


    /// Threading, Fetch and Wrapping Data
    
    // Summary:
    //     Returns a GRPC Compatible IEnumerableList that meets the requirements of protos
    //     
    //     Due GetImgURLSync(), This is added manually for a multi-threaded approach
    // Parameters:
    //   value:
    //     A Collection of Backlog Entries from the SQLite Database
    //
    // Returns:
    //     Returns a GRPC Compatible IEnumerableList that meets the requirements of protos
    private RepeatedField<BacklogItem> GetGRPCGameList(IList<GameListDB.Model.Backlog> beatenGames)
    {
        IList<Thread> ThreadList = new List<Thread>();
        Google.Protobuf.Collections.RepeatedField<BacklogItem> output = new();
        foreach (var game in beatenGames)
        {
            while (ThreadList.Count() > MaxThread)
            {
                RemoveFinishedThreadsAndSleep(ref ThreadList);

            }

            ThreadList.Add(new Thread(AddGameListOutputAsync));
            ThreadList.Last().Start(new ThreadProperties(ref output, game));
        }

        while (ThreadList.Count() != 0)
        {
            RemoveFinishedThreadsAndSleep(ref ThreadList);
        }

        return output;
    }

    public void AddGameListOutputAsync(object? objects)
    {
        if (objects == null) { return; }
        ThreadProperties structs = (ThreadProperties)objects;
        structs.output.Add(new BacklogItem()
        {
            Name = structs.game.Name,
            Status = structs.game.Status,
            Releaseyear = structs.game.Releaseyear.ToString(),
            Plataform = structs.game.Plataform,
            Nsfw = structs.game.Nsfw == null ? 0 : (int)structs.game.Nsfw,
            Img = GetImgURLSync(structs.game)
        }
        );
    }

    //
    // Summary:
    //     Returns the BoxArt URL related to the Game (If It has igdbID)
    //     
    //     Warning: This code is slow because the Network IO and forced the synchronous code for 
    //     avoid change the signature of EndPoints (Which It's auto-generated Via Protos) 
    //     Highly recommended the use of MultiThreading 
    // Parameters:
    //   value:
    //     Game to fetch the BoxArt
    //
    // Returns:
    //     ImgURL related to the BoxArt
    private string GetImgURLSync(GameListDB.Model.Backlog game)
    {
        return igdb.GetBoxArtURL(serviceProvider.GetRequiredService<GameListsContext>().GetIgdbId(game)).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static void RemoveFinishedThreadsAndSleep(ref IList<Thread> ThreadList)
    {
        Thread.Sleep(MillisecondsTimeout);

        for (byte i = 0; i < ThreadList.Count(); i++)
        {
            if (!ThreadList.ElementAt(i).IsAlive)
            {
                ThreadList.RemoveAt(i);
            }
        }
    }

}