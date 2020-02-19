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

        public static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine(" Pokemon TCG Scrapper ".PadLeft(80, '-').PadRight(150, '-'));
                Console.WriteLine("1. Fetch cards");
                Console.WriteLine("2. Show amount of cards already fetched");
                Console.WriteLine("3. Leave");
                Console.Write("Choose: ");
                
                try
                {
                    var choice = int.Parse(Console.ReadLine() ?? throw new Exception());
                    if (choice <= 0) throw new Exception();

                    switch (choice)
                    {
                        case 3:
                            return;
                        
                        case 2:
                            ShowCardsCount();
                            Console.ReadLine();
                            break;
                        
                        case 1:
                            Fetch();
                            break;
                        
                        default:
                            throw new Exception();
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid input. Press any key to try again.");
                    Console.ReadLine();
                    Console.Clear();
                }
            }
        }

        private static void ShowCardsCount()
        {
            Directory.CreateDirectory(CardsDirectoryPath);
            
            try
            {
                using var reader = new StreamReader(SingleFilePath);
                var cards = JsonConvert.DeserializeObject<ICollection<CardModel>>(reader.ReadToEnd());
                Console.WriteLine($"You've saved {cards.Count} cards locally.");
            }
            catch (Exception)
            {
                Console.WriteLine("You don't have any saved cards yet. (Press any key to return to menu)");
            }
        }

        private static void Fetch()
        {
            Console.WriteLine("Loading...");
            
            SetAmountOfCards();
            _cardsUrls = new BlockingCollection<string>(_amountOfCards);
            _cardModels = new BlockingCollection<CardModel>(_amountOfCards);
            
            Console.WriteLine($"There are {_amountOfCardListPages} pages, each containing up to 12 cards.");
            Console.Write("Want to fetch'em now? (y/n) ");
            var shouldFetchCards = Console.ReadLine()?.ToLower() == "y";
            
            if (!shouldFetchCards) return;
            
            SetAmountOfPages();
            SetSavingMode();
            FetchCards();
        }

        private static void SetAmountOfPages()
        {
            while (true)
            {
                try
                {
                    Console.Clear();
                    Console.Write("How many? ");

                    var amountOfPages = int.Parse(Console.ReadLine() ?? throw new Exception());
                    if (amountOfPages < 0)
                        throw new Exception();

                    SetAmountOfCards(amountOfPages);
                    break;
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid input. (press any key to try again)");
                    Console.Read();
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
                    
                    if (input != "s" && input != "m")
                        throw new Exception();

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
                    Console.Read();
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
            {
                _amountOfCardListPages = pages;
            }
            
            _amountOfCards = _amountOfCardsInList * _amountOfCardListPages;

        }

        private static void FetchCards()
        {
            Task.Run(GetCardsUrls);
            Task.Run(MapCardModels);
            Task.Run(SaveCards).Wait();

            Console.Write("Finished fetching the cards, press any key to return to menu.");
            Console.ReadLine();
        }

        private static void GetCardsUrls()
        {
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

                    lock ((object)amountProcessed)
                    {
                        amountProcessed++;
                    }
                }
            });

            _cardsUrls.CompleteAdding();
            progressTrackingTask.Wait();
        }

        private static void MapCardModels()
        {
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
                        var cardModel = Mapper.CardModelFrom(new HtmlWeb().Load(cardUrl), cardUrl);
                        _cardModels.Add(cardModel);
                        amountProcessed++;
                    }, TaskCreationOptions.AttachedToParent));
                }
                
                // Starts the batch tasks and wait for them to finish
                tasks.ForEach(task => task.Start());
                var tasksFinished = Task.WhenAll(tasks);
                tasksFinished.Wait();
                
                // Increments batchesCount
                batchesCount++;
            }

            _cardModels.CompleteAdding();
            progressTrackingTask.Wait();
        }

        private static void SaveCards()
        {
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
            
            Directory.CreateDirectory(CardsDirectoryPath);

            if (_saveInSingleFile)
            {
                if (!File.Exists(SingleFilePath)) File.Open(SingleFilePath, FileMode.Create, FileAccess.ReadWrite);
                
                Task.WhenAll(
                    Task.Run(() => SaveCardsInSingleFile(ref amountProcessed)), 
                    Task.Run(() => SaveCardsInSingleFile(ref amountProcessed))
                ).Wait();
            }
            else
            {
                Task.WhenAll(
                    Task.Run(() => SaveEachCardInItsOwnFile(ref amountProcessed)), 
                    Task.Run(() => SaveEachCardInItsOwnFile(ref amountProcessed))
                ).Wait(); 
            } 

            progressTrackingTask.Wait();
        }

        private static void SaveEachCardInItsOwnFile(ref int amountProcessed)
        {
            while (!_cardModels.IsCompleted)
            {
                if (_cardModels.Count <= 0) continue;

                var cardModel = _cardModels.Take();
                var cardFilePath = GetCardFilePath(cardModel.Name);
                File.WriteAllText(cardFilePath, JsonConvert.SerializeObject(cardModel, Formatting.Indented));
                
                lock ((object) amountProcessed)
                {
                    amountProcessed++;
                }
            }
        }

        private static string GetCardFilePath(string modelName) => string.Concat(CardsDirectoryPath, modelName, ".json");

        private static void SaveCardsInSingleFile(ref int amountProcessed)
        {
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

                    lock ((object) amountProcessed) amountProcessed++;
                }
            }
        }
    }
}