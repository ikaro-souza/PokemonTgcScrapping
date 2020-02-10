using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Models;

namespace App.ConsoleApp
{
    class App
    {
        private const string BaseUrl = "https://www.pokemon.com/us/pokemon-tcg/pokemon-cards/?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=";
        private static BlockingCollection<CardModel> Cards;
        private static 
        static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            var amountOfPages = _GetAmountOfCardPages();
            Console.WriteLine($"There are {amountOfPages} cards to be fetched.");
            
            while(true)
            {
                var confirm = _GetUserInput("Would you like to fetch the cards now (y/n)? ").ToString();
                if (confirm.Length > 1 || !(confirm.StartsWith('y') || confirm.StartsWith('n')))
                {
                    _ShowError("Invalid input");
                }
                else if (confirm.StartsWith('n'))
                {
                    break;
                }
                else
                {
                    _FetchCardList();
                }
            }
        }

        private static void _ShowError(string errorMessage)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage);
            Console.ResetColor();
        }

        private static object _GetUserInput(string message)
        {
            Console.Write(message);
            return Console.ReadLine();
        }

        private static int _GetAmountOfCardPages()
        {
            _ShowLoadingMessage();
            
            var cardListWebDocument = new HtmlWeb().Load(BaseUrl);
            var element = cardListWebDocument.GetElementbyId("cards-load-more");
            var numOfPages = int.Parse(element.Element("div").Element("span").InnerText.Substring(4));
            
            Console.Clear();
            return 12 * numOfPages; // 12 is the number of cards on a page
        }

        private static void _ShowLoadingMessage()
        {
            Console.WriteLine("Loading... (Press ctrl+c to exit the application)");
        }

        private static void _FetchCardList()
        {
            _ShowLoadingMessage();
            Cards = new BlockingCollection<CardModel>();
        }

        private static int _GetCardDetail()
        {
            return 0;
        }

    }
}
