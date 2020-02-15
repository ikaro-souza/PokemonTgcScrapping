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

        private static BlockingCollection<string> _cardsUrls;
        private static BlockingCollection<CardModel> _cardModels;
        private static int _amountOfCardsInList;

        public static void Main(string[] args)
        {
            Console.WriteLine("EAE");
            Console.ReadLine();
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
                            Run();
                            break;
                        
                        case 1:
                            Fetch();
                            Run();
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
            try
            {
                using var reader = new StreamReader(File.OpenRead("./PokemonCards.json"));
                var cards = JsonConvert.DeserializeObject<IEnumerable<CardModel>>(reader.ReadToEnd());
                Console.WriteLine($"You've saved {cards.Count()} locally.");
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
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
            
            if (!shouldFetchCards)
            {
                Console.WriteLine("Alright, see you later.");
                return;
            }
            
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
            
            FetchCards();
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
        }

        private static void GetCardsUrls()
        {
            var amountProcessed = 0;
            var progressTrackingTask = Task.Run(() =>
            {
                while (!_cardsUrls.IsAddingCompleted)
                {
                    Thread.Sleep(100);

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
            var progressTrackingTask = Task.Run(() =>
            {
                while (!_cardModels.IsAddingCompleted)
                {
                    Thread.Sleep(100);

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

        static void SaveCards()
        {
            var amountProcessed = 0;
            var progressTrackingTask = Task.Run(() =>
            {
                while (amountProcessed < _amountOfCards)
                {
                    if (_cardModels.IsAddingCompleted && amountProcessed == 0) break;
                    
                    Thread.Sleep(100);

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
                
                using var readStream = File.Open("./PokemonCards.json", FileMode.OpenOrCreate, FileAccess.Read);
                using var streamReader = new StreamReader(readStream);
                var contentAlreadyInFile = streamReader.ReadToEnd();
                        
                var cards = JsonConvert.DeserializeObject<List<CardModel>>(contentAlreadyInFile);
                cards ??= new List<CardModel>();
                var cardModel = _cardModels.Take();
                
                if (cards.Any(card => card.Code == cardModel.Code)) continue;
                cards.Add(cardModel);
                
                readStream.Close();
                readStream.Dispose();
                
                using var writeStream = File.Open("./PokemonCards.json", FileMode.Create, FileAccess.Write);
                using var streamWriter = new StreamWriter(writeStream);
                streamWriter.Write(JsonConvert.SerializeObject(cards.OrderBy(card => card.Expansion).ThenBy(card => card.Name)));
                        
                lock ((object) amountProcessed)
                {
                    amountProcessed++;
                }
            }
            
            progressTrackingTask.Wait();
        }
    }
}