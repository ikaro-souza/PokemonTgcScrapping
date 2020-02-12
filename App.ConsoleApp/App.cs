using HtmlAgilityPack;
using Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.ConsoleApp
{
    class App
    {
        private static readonly string _baseUrl = "https://www.pokemon.com";
        private static int _numberOfPages = 0;
        private static int _numberOfCards = 0;

        private static BlockingCollection<string> _fetchCardPageUrls;
        private static BlockingCollection<Task> _cardModelingTasks;
        private static BlockingCollection<CardModel> _cardModels;

        static void Main(string[] args)
        {
            ShowLoadingMessage("Getting current amount of cards");
            SetNumberOfCards();

            _fetchCardPageUrls = new BlockingCollection<string>(_numberOfCards);
            _cardModelingTasks = new BlockingCollection<Task>(_numberOfCards);
            _cardModels = new BlockingCollection<CardModel>(_numberOfCards);
            
            Console.WriteLine($"There are currently about {_numberOfCards} cards in the game.");
            Console.Write("Would you like to fetch'em now? (y/n) ");
            var answer = Console.ReadLine();

            if (answer != "y")
            {
                Console.WriteLine("Alright then, bye.");
                return;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            FetchCards();

            stopwatch.Stop();
            Console.WriteLine($"Fetched {(_numberOfCards / stopwatch.Elapsed.TotalSeconds).ToString("n2")} cards/s.");
        }

        private static void SetNumberOfCards()
        {
            var cardListPage = GetCardsListPage(1);
            var cardListItems = GetCardsListItems(cardListPage);
            //_numberOfPages = int.Parse(cardListPage
            //    .QuerySelector("#cards-load-more > div > span")
            //    .InnerText
            //    .Substring(5));
            _numberOfPages = 10;
            _numberOfCards = _numberOfPages * cardListItems.Count;
        }

        private static string GetCardListPageUrl(int pageNumber)
        {
            return string.Concat(_baseUrl, $"/us/pokemon-tcg/pokemon-cards/{pageNumber}?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=");
        }

        private static HtmlDocument GetCardsListPage(int pageNumber)
        {
            return new HtmlWeb().Load(GetCardListPageUrl(pageNumber));
        }

        private static IList<HtmlNode> GetCardsListItems(HtmlDocument cardListPage)
        {
            return cardListPage.QuerySelectorAll("#cardResults li");
        }

        private static IList<HtmlNode> GetCardsListItems(int pageNumber)
        {
            return GetCardsListPage(pageNumber).QuerySelectorAll("#cardResults li");
        }

        private static void ShowLoadingMessage(string message)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"{message}...");
            Console.ResetColor();
        }

        private static void ResetConsoleCursorPosition()
        {
            Console.SetCursorPosition(0, 0);
        }

        private static void ShowTaskProgress(string message, int currentCount, int maxCount)
        {
            message = $"{message} ({currentCount} of {maxCount})";
            ShowLoadingMessage(message);
        }

        private static void FetchCards()
        {
            FetchCardPageUrls();
            MapCardModels();
        }

        private static void FetchCardPageUrls()
        {
            ShowLoadingMessage("Fetching card page urls");

            Parallel.For(1, _numberOfPages + 1, pageNumber =>
            {
                var cardListItems = GetCardsListItems(pageNumber);
                foreach (var cardListItem in cardListItems)
                {
                    var cardPageUrl = string.Concat(_baseUrl,cardListItem.QuerySelector("a").Attributes["href"].Value);
                    _fetchCardPageUrls.Add(cardPageUrl);
                }
            });

            _fetchCardPageUrls.CompleteAdding();
            Console.WriteLine("Done fetching the cards urls");
        }

        private static void MapCardModels()
        {
            Task.Run(() =>
            {
                while (!_cardModelingTasks.IsCompleted)
                {
                    Thread.Sleep(100);
                    ShowTaskProgress("Mapping card models", _cardModelingTasks.Count, _numberOfCards);
                }
            });

            var tasksGeneratorTask = Task.Run(GenerateCardModelingTasks);
            var cardModelMappingTask = Task.Run(ExecuteCardModelingTasks);

            _cardModels.CompleteAdding();
        }

        private static void GenerateCardModelingTasks()
        {
            static CardModel MapCardModel(string cardPageUrl)
            {
                var cardPage = new HtmlWeb().Load(cardPageUrl);
                return Mapper.CardModelFrom(cardPage);
            }

            foreach (var cardPageUrl in _fetchCardPageUrls)
            {
                _cardModelingTasks.Add(new Task(() =>
                {

                    var cardModel = MapCardModel(cardPageUrl);
                    //Console.WriteLine(JsonConvert.SerializeObject(cardModel));
                    _cardModels.Add(cardModel);
                }, TaskCreationOptions.AttachedToParent));
            }

            _cardModelingTasks.CompleteAdding();
        }

        private static void ExecuteCardModelingTasks()
        {
            var tasksResult = Task.WhenAll(_cardModelingTasks);

            while (!_cardModelingTasks.IsCompleted)
            {
                Thread.Sleep(50);

                if (_cardModelingTasks.Count < 12)
                {
                    if (!_cardModelingTasks.IsAddingCompleted)
                        continue;
                }

                var amountOfTasksToTake = _cardModelingTasks.Count >= 12 ? 12 : _cardModelingTasks.Count;
                for (int i = 0; i < amountOfTasksToTake; i++)
                {
                    var task = _cardModelingTasks.Take();
                    task.Start();
                }
            }

            tasksResult.Wait();
            _cardModels.CompleteAdding();
        }
    }
}