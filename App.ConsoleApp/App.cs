using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Models;
using Newtonsoft.Json;

namespace App.ConsoleApp
{ 
    class App
    {
        private const string BaseUrl = "https://www.pokemon.com";
        private static readonly HtmlNode CardListDocument = new HtmlWeb().Load(GetCardListPageUrl()).DocumentNode;
        private static BlockingCollection<string> _cardDetailsPageUrls;
        private static BlockingCollection<HtmlDocument> _cardDetailsPages;
        private static BlockingCollection<CardModel> _cardModels;
        private static int _amountOfPages;
        private static int _amountOfCards;

        static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            SetAmountOfPages();
            _amountOfCards = GetAmountOfCards();

            Console.WriteLine($"There are about {_amountOfCards} cards to be fetched.");
            GetUserInput("Press any key to start fetching.");
            Console.Clear();

            _cardDetailsPageUrls = new BlockingCollection<string>(_amountOfCards);
            _cardDetailsPages = new BlockingCollection<HtmlDocument>(_amountOfCards);
            _cardModels = new BlockingCollection<CardModel>(_amountOfCards);

            var startTime = DateTime.Now;

            Task.Run(CardsUrlProducers).Wait();
            Task.Run(CardModelsProducers);
            Task.Run(CardModelsConsumers);

            ShowElapsedTime("Application", startTime);
            Console.ReadLine();
        }

        private static void ShowTaskProgress(string taskName, int currentItemsCount, int totalItemsCount)
        {
            Console.SetCursorPosition(0, 0);
            Console.Write("", Console.WindowWidth);
            Console.SetCursorPosition(0, 1);
            Console.Write("", Console.WindowWidth);
            Console.SetCursorPosition(0, 0);

            ShowLoadingMessage(taskName);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"{currentItemsCount} of {totalItemsCount}");
            Console.ResetColor();
        }

        // Scraps each list of cards and puts each card's url on a queue 
        private static void CardsUrlProducers()
        {
            try
            {
                Task.Run(() =>
                {
                    var startTime = DateTime.Now;
                    var taskName = "Fetching cards urls";

                    while (!_cardDetailsPageUrls.IsAddingCompleted)
                    {
                        Thread.Sleep(100);
                        ShowTaskProgress(taskName, _cardDetailsPageUrls.Count, _amountOfCards);
                    }

                    ShowElapsedTime(taskName, startTime);
                    return;
                });

                Parallel.For(1, _amountOfPages, (pageNumber, loopState) =>
                {
                    var pageUrl = GetCardListPageUrl(pageNumber);
                    var tries = 0;
                    HtmlDocument cardListPageDocument = null;
                    while (cardListPageDocument is null && tries < 5)
                    {
                        Thread.Sleep(100);
                        cardListPageDocument = new HtmlWeb().Load(pageUrl);
                    }

                    if (cardListPageDocument is null)
                        ShowError($"error fetching page {pageNumber}");

                    var cardListItems = cardListPageDocument.QuerySelectorAll("ul#cardResults li");

                    Parallel.ForEach(cardListItems, (cardListItem, loopState) =>
                    {
                        var cardPageUrl = BaseUrl + cardListItem
                        .QuerySelector("a")
                        .GetAttributeValue("href", "");

                        _cardDetailsPageUrls.Add(cardPageUrl);
                    });
                });

                _cardDetailsPageUrls.CompleteAdding();
            }
            catch (Exception e)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
        }

        private static void ShowError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            throw new Exception(message);
        }

        // Maps each card details page to a CardModel and put it on a queue
        private static void CardModelsProducers()
        {

            Task.Run(() =>
            {
                Thread.Sleep(1500);
                Console.Clear();
                var startTime = DateTime.Now;
                var taskName = "Mapping cards models";

                while (!_cardModels.IsAddingCompleted)
                {
                    Thread.Sleep(100);
                    ShowTaskProgress(taskName, _cardModels.Count, _amountOfCards);
                }

                ShowElapsedTime(taskName, startTime);
            });

            Parallel.For(0, _amountOfCards, (i, loopState) =>
            {
                while (!_cardDetailsPageUrls.IsCompleted)
                {
                    if (_cardDetailsPageUrls.Count <= 0) continue;

                    var cardPageUrl = _cardDetailsPageUrls.Take();
                    var tries = 0;
                    HtmlDocument cardPage = null;

                    while (cardPage is null && tries < 5)
                    {
                        Thread.Sleep(100);
                        cardPage = new HtmlWeb().Load(cardPageUrl);
                    }

                    if (cardPage is null)
                        ShowError("error fetching card");

                    var cardModel = Mapper.CardModelFrom(cardPage.DocumentNode);
                    _cardModels.Add(cardModel);
                }
            });

            _cardModels.CompleteAdding();
        }

        // Serializes each CardModel and saves it in the cards.json file
        private static void CardModelsConsumers()
        {
            var startTime = DateTime.Now;
            var taskName = "Saving cards to file";
            var pagesCount = 0;
            Thread.Sleep(1500);
            Console.Clear();

            Directory.CreateDirectory("cards");

            while (!_cardModels.IsCompleted)
            {
                if (_cardModels.Count <= 12) continue;

                pagesCount++;    
                using var fileStreamWriter = File.AppendText($"./cards/cards{pagesCount.ToString().PadLeft(3, '0')}.json");
                var cards = new List<CardModel>();

                while (cards.Count < 12)
                {
                    cards.Add(_cardModels.Take());
                }

                var jsonCards = JsonConvert.SerializeObject(cards, Formatting.Indented);
                fileStreamWriter.Write(jsonCards);
            }

            ShowElapsedTime(taskName, startTime);
        }

        private static object GetUserInput(string message)
        {
            Console.Write(message);
            return Console.ReadLine();
        }

        private static void SetAmountOfPages()
        {
            var element = CardListDocument.QuerySelector("div#cards-load-more > div > span");
            var numOfPages = int.Parse(element.InnerText.Substring(4));
            _amountOfPages = numOfPages;
        }

        private static int GetAmountOfCards()
        {
            ShowLoadingMessage();
            
            Console.Clear();
            return 12 * _amountOfPages; // 12 is the max number of cards on a page
        }

        private static string GetCardListPageUrl(int pageNumber = 1)
        {
            return pageNumber != 1
                ? BaseUrl +
                  $"/us/pokemon-tcg/pokemon-cards/{pageNumber}?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist="
                : BaseUrl +
                  "/us/pokemon-tcg/pokemon-cards/?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=";
        }

        private static void ShowLoadingMessage(string text = "Loading")
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"{text}... (Press ctrl+c to exit the application)");
            Console.ResetColor();
        }

        private static void ShowElapsedTime(string taskName, DateTime startTime)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{taskName} task finished after {(DateTime.Now - startTime).TotalSeconds} seconds.");
            Console.ResetColor();
        }
    }

    
}
