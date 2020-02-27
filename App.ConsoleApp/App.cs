using HtmlAgilityPack;
using Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.ConsoleApp
{
    class App
    {
        private static readonly object ConsoleWriterLock = new object();

        private const string BaseUrl = "https://www.pokemon.com";
        private static int _amountOfCardListPages;
        private static int _amountOfCards;

        private const string CardsDirectoryPath = "./cards/";
        private const string SingleFilePath = CardsDirectoryPath + "PokemonCards.json";
        private static readonly object SingleFileLocker = new object();
        private static bool _saveInSingleFile;

        private static BlockingCollection<string> _cardsUrls;
        private static BlockingCollection<CardModel> _cardModels;
        private static int _amountOfCardsInList;

        public static async Task Main(string[] args)
        {
            await Run();
        }

        private static async Task Run()
        {
            while (true)
            {
                Console.Clear();
                ShowAppName();
                Console.WriteLine("1. Fetch cards");
                Console.WriteLine("2. Leave");
                Console.Write("Choose: ");
                
                try
                {
                    var choice = int.Parse(Console.ReadLine() ?? throw new Exception());
                    if (choice <= 0 || choice > 2) throw new Exception();
                    if (choice == 2) return;

                    break;
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid input. Press any key to try again.");
                    Console.ReadLine();
                    Console.Clear();
                }
            }

            await Fetch();
        }

        private static void ShowAppName()
        {
            var appName = "Pokemon TCG Scrapper";
            var windowWidth = Console.WindowWidth;
            var leftPadding = new string('-', (windowWidth / 2) - appName.Length) + " ";
            var rigthPadding = " " + new string('-', (windowWidth / 2) - appName.Length) ;
            Console.WriteLine($"{leftPadding}{appName}{rigthPadding}");
        }

        private static async Task Fetch()
        {
            Console.WriteLine("Loading...");
            
            SetAmountOfCards();
            _cardsUrls = new BlockingCollection<string>(_amountOfCards);
            _cardModels = new BlockingCollection<CardModel>(_amountOfCards);
            
            SetAmountOfPagesToFetch();
            SetSavingMode();
            await FetchCards();
        }

        private static void SetAmountOfPagesToFetch()
        {
            while (true)
            {
                try
                {
                    Console.WriteLine($"There are {_amountOfCardListPages} pages, each containing up to 12 cards.");
                    Console.Write("How many pages do you want to fetch? ");

                    var amountOfPages = int.Parse(Console.ReadLine() ?? throw new Exception());
                    if (amountOfPages < 1 || amountOfPages > _amountOfCardListPages)
                        throw new Exception();

                    SetAmountOfCards(amountOfPages);
                    Console.Clear();
                    break;
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid input. (press any key to try again)");
                    Console.Read();
                    Console.Clear();
                }
            }
        }

        private static void SetSavingMode()
        {
            while (true)
            {
                try
                {
                    Console.Write("How do you want to save the cards? (\"s\" for single file or \"m\" multiple) ");
                    var input = Console.ReadLine()?.ToLower() ?? throw new Exception();
                    
                    if (input != "s" && input != "m") throw new Exception();

                    _saveInSingleFile = input switch
                    {
                        "s" => true,
                        "m" => false,
                        _ => throw new Exception()
                    };
                    
                    break;
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid input. (press any key to try again)");
                    Console.ReadLine();
                    Console.Clear();
                }
            }
        }

        private static string GetCardListPageUrl(int pageNumber = 1)
        {
            return BaseUrl +
                   $"/us/pokemon-tcg/pokemon-cards/{pageNumber}?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=";
        }

        private static string GetCardPageUrl(string cardUrl)
        {
            return BaseUrl + cardUrl;
        }

        private static void SetAmountOfCards(int pages = -1)
        {
            if (pages < 0)
            {
                var cardListPage = new HtmlWeb().Load(GetCardListPageUrl());
                var amountOfPages = int.Parse(
                    cardListPage
                        .QuerySelector("div#cards-load-more > div > span")
                        .InnerText
                        .Substring(5)
                );
                _amountOfCardsInList = cardListPage.QuerySelectorAll("#cardResults li").Count;
                _amountOfCardListPages = amountOfPages;
            }
            else
                _amountOfCardListPages = pages;
            
            _amountOfCards = _amountOfCardsInList * _amountOfCardListPages;
        }

        private static async Task FetchCards()
        {
            _ = GetCardsUrls();
            _ = MapCardModels();
            await Task.Run(SaveCards);

            Console.Write("Finished fetching the cards, press any key to return to menu.");
            Console.ReadLine();
        }

        private static async Task GetCardsUrls()
        {
            var amountProcessedLock = new object();
            var amountProcessed = 0;
            var progressTrackingTask = Task.Run(async () =>
            {
                while (!_cardsUrls.IsAddingCompleted)
                {
                    await Task.Delay(100);

                    lock (ConsoleWriterLock)
                    {
                        Console.SetCursorPosition(0, 0);
                        Console.Write($"Fetching cards urls... {amountProcessed} of {_amountOfCards}".PadRight(Console.WindowWidth, ' '));
                    }
                }

                lock (ConsoleWriterLock)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.Write("Fetching cards urls completed!".PadRight(Console.WindowWidth, ' '));
                }
            });

            Parallel.For(1, _amountOfCardListPages + 1, pageNumber =>
            {
                var pageDocument = new HtmlWeb().Load(GetCardListPageUrl(pageNumber));
                var cardListItems = pageDocument.QuerySelectorAll("#cardResults li");

                foreach (var cardListItem in cardListItems)
                {
                    var cardUrl = cardListItem
                        .QuerySelector("a")
                        .Attributes["href"].Value;
                    cardUrl = GetCardPageUrl(cardUrl);
                    _cardsUrls.Add(cardUrl);

                    lock (amountProcessedLock) amountProcessed++;
                }
            });

            _cardsUrls.CompleteAdding();
            await progressTrackingTask;
        }

        private static async Task MapCardModels()
        {
            var amountProcessedLock = new object();
            var amountProcessed = 0;
            var progressTrackingTask = Task.Run(async () =>
            {
                while (!_cardModels.IsAddingCompleted)
                {
                    if (_cardModels.IsCompleted && amountProcessed < _cardModels.Count) break;
                    
                    await Task.Delay(100);

                    lock (ConsoleWriterLock)
                    {
                        Console.SetCursorPosition(0, 1);
                        Console.Write($"Mapping cards models... {amountProcessed} of {_amountOfCards}".PadRight(Console.WindowWidth, ' '));
                    }
                }

                lock (ConsoleWriterLock)
                {
                    Console.SetCursorPosition(0, 1);
                    Console.Write("Mapping card models completed!".PadRight(Console.WindowWidth, ' '));
                }
            });

            var batchTasksCount = _amountOfCards / 4;
            var batchesCount = 1;
            while (batchesCount <= 4)
            {
                // Creates and populates the tasks list
                var tasks = new List<Task>(batchTasksCount);
                while (tasks.Count < batchTasksCount)
                {
                    tasks.Add(new Task(() =>
                    {
                        var cardUrl = _cardsUrls.Take();
                        var cardModel = Mapper.CardModelFrom(new HtmlWeb().Load(cardUrl));
                        _cardModels.Add(cardModel);

                        lock (amountProcessedLock) amountProcessed++;
                    }, TaskCreationOptions.AttachedToParent));
                }
                
                // Starts the batch tasks and wait for them to finish
                tasks.ForEach(task => task.Start());
                await Task.WhenAll(tasks);
                
                // Increments batchesCount
                batchesCount++;
            }

            _cardModels.CompleteAdding();
            await progressTrackingTask;
        }

        private static async Task SaveCards()
        {
            Directory.CreateDirectory(CardsDirectoryPath);
            File.Open(SingleFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite).Dispose();

            if (_saveInSingleFile)
            {
                if (!File.Exists(SingleFilePath)) File.Open(SingleFilePath, FileMode.Create, FileAccess.ReadWrite);

                await Task.WhenAll(
                    SaveCardsInSingleFile(),
                    SaveCardsInSingleFile()
                );
            }
            else
            {
                await Task.WhenAll(
                    SaveEachCardInItsOwnFile(),
                    SaveEachCardInItsOwnFile()
                );
            }
        }

        private static async Task SaveEachCardInItsOwnFile()
        {
            var amountProcessedLock = new object();
            var amountProcessed = 0;

            var progressTrackingTask = Task.Run(async () =>
            {
                while (amountProcessed < _amountOfCards)
                {
                    if (_cardModels.IsCompleted) break;

                    await Task.Delay(100);

                    lock (ConsoleWriterLock)
                    {
                        Console.SetCursorPosition(0, 2);
                        Console.WriteLine($"Saving cards to file... {amountProcessed} of {_amountOfCards}".PadRight(Console.WindowWidth, ' '));
                    }
                }

                lock (ConsoleWriterLock)
                {
                    Console.SetCursorPosition(0, 2);
                    Console.WriteLine("Saving cards to file completed!".PadRight(Console.WindowWidth, ' '));
                }
            });

            while (!_cardModels.IsCompleted)
            {
                if (_cardModels.Count <= 0) continue;

                var cardModel = _cardModels.Take();
                var cardFilePath = GetCardFilePath(cardModel.Code, cardModel.Name);
                File.WriteAllText(cardFilePath, JsonConvert.SerializeObject(cardModel, Formatting.Indented));
                
                lock (amountProcessedLock) amountProcessed++;
            }

            await progressTrackingTask;
        }

        private static string GetCardFilePath(string modelCode, string modelName) => string.Concat(CardsDirectoryPath, modelCode, " - ", modelName, ".json");

        private static async Task SaveCardsInSingleFile()
        {
            var amountProcessedLock = new object();
            var amountProcessed = 0;

            var progressTrackingTask = Task.Run(async () =>
            {
                while (amountProcessed < _amountOfCards)
                {
                    if (_cardModels.IsCompleted) break;

                    await Task.Delay(100);

                    lock (ConsoleWriterLock)
                    {
                        Console.SetCursorPosition(0, 2);
                        Console.WriteLine($"Saving cards to file... {amountProcessed} of {_amountOfCards}".PadRight(Console.WindowWidth, ' '));
                    }
                }

                lock (ConsoleWriterLock)
                {
                    Console.SetCursorPosition(0, 2);
                    Console.WriteLine("Saving cards to file completed!".PadRight(Console.WindowWidth, ' '));
                }
            });

            while (!_cardModels.IsCompleted)
            {
                if (_cardModels.Count <= 0) continue;

                lock (SingleFileLocker)
                {
                    var cardsFileStreamReader = new StreamReader(SingleFilePath);
                    var cardsInFile = cardsFileStreamReader.ReadToEnd();
                    cardsFileStreamReader.Close();
                    cardsFileStreamReader.Dispose();
                    
                    var cards = JsonConvert.DeserializeObject<List<CardModel>>(cardsInFile);
                    cards ??= new List<CardModel>();
                    var cardModel = _cardModels.Take();

                    if (cards.Any(card => card.Code == cardModel.Code)) continue;
                    
                    cards.Add(cardModel);
                    cards = cards.OrderBy(card => card.Expansion).ThenBy(card => card.Name).ToList();
                    
                    File.WriteAllText(SingleFilePath, JsonConvert.SerializeObject(cards));

                    lock (amountProcessedLock) amountProcessed++;
                }
            }

            await progressTrackingTask;
        }
    }
}