using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            GetUserInput("Would you like to fetch the cards now (y/n)? ");

            _cardDetailsPageUrls = new BlockingCollection<string>(_amountOfCards);
            _cardDetailsPages = new BlockingCollection<HtmlDocument>(_amountOfCards);
            _cardModels = new BlockingCollection<CardModel>(_amountOfCards);

            Task.Run(CardsUrlProducers);
            Task.Run(CardsUrlConsumers);
            //Task.Run(CardModelsProducers);
            //Task.Run(CardModelsConsumers);

            Console.ReadLine();
        }

        // Scraps each list of cards and puts each card's url on a queue 
        private static void CardsUrlProducers()
        {
            ShowLoadingMessage("Fetching cards urls");
            var startTime = DateTime.Now;

            Parallel.For(1, _amountOfPages, (pageNumber, loopState) =>
            {
                var pageUrl = GetCardListPageUrl(pageNumber);
                var cardListPageDocument = new HtmlWeb().Load(pageUrl);
                var cardListItems = cardListPageDocument.QuerySelectorAll("ul#cardResults li");
                foreach (var cardListItem in cardListItems)
                {
                    var cardPageUrl = BaseUrl + cardListItem
                        .QuerySelector("a")
                        .GetAttributeValue("href", "");
                    if (cardPageUrl != string.Empty)
                        _cardDetailsPageUrls.Add(cardPageUrl);
                }
            });

            _cardDetailsPageUrls.CompleteAdding();
            ShowElapsedTime("CardsUrlProducers", startTime);
        }

        // Scraps each card details page and puts its document on a queue
        private static void CardsUrlConsumers()
        {
            ShowLoadingMessage("Fetching cards pages");
            var startTime = DateTime.Now;

            Parallel.For(0, _amountOfPages, (pageNumber, loopState) =>
            {
                while(!_cardDetailsPageUrls.IsCompleted)
                {
                    Thread.Sleep(10);
                    if (_cardDetailsPageUrls.Count <= 0) continue;

                    var cardPageUrl = _cardDetailsPageUrls.Take();
                    var cardPageDocument = new HtmlWeb().Load(cardPageUrl);
                    _cardDetailsPages.Add(cardPageDocument);
                }
            });

            _cardDetailsPages.CompleteAdding();
            ShowElapsedTime("CardsUrlConsumers", startTime);
        }

        // Maps each card details page to a CardModel and put it on a queue
        private static void CardModelsProducers()
        {
            ShowLoadingMessage("Mapping cards models");
            var startTime = DateTime.Now;

            Parallel.For(0, _amountOfPages, (i, loopState) =>
            {
                while (!_cardDetailsPages.IsCompleted)
                {
                    Thread.Sleep(10);
                    if (_cardDetailsPages.Count <= 0) continue;

                    var cardPage = _cardDetailsPages.Take();
                    var cardModel = Mapper.CardModelFrom(cardPage.DocumentNode);
                    _cardModels.Add(cardModel);
                }
            });

            _cardModels.CompleteAdding();
            ShowElapsedTime("CardModelsProducers", startTime);
        }

        // Serializes each CardModel and saves it in the cards.json file
        private static void CardModelsConsumers()
        {
            ShowLoadingMessage("Saving cards to file");
            var startTime = DateTime.Now;

            while (!_cardModels.IsCompleted)
            {
                Thread.Sleep(10);
                if (_cardModels.Count <= 0) continue;

                Console.WriteLine(JsonConvert.SerializeObject(_cardModels.Take(), Formatting.Indented));
            }

            ShowElapsedTime("CardModelsConsumers", startTime);
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
            _amountOfPages = 100;
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
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"{text}... (Press ctrl+c to exit the application)");
            Console.ResetColor();
        }

        private static void ShowElapsedTime(string taskName, DateTime startTime)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{taskName} task finished after {(DateTime.Now - startTime).Seconds} seconds.");
            Console.ResetColor();
        }
    }

    
}
