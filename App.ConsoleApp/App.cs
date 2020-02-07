using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace App.ConsoleApp
{
    class App
    {
        static private readonly string baseUrl = "https://www.pokemon.com/us/pokemon-tcg/pokemon-cards/?cardName=&cardText=&evolvesFrom=&simpleSubmit=&format=unlimited&hitPointsMin=0&hitPointsMax=340&retreatCostMin=0&retreatCostMax=5&totalAttackCostMin=0&totalAttackCostMax=5&particularArtist=";
        
        static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            while(true)
            {
                var ammountOfPages = _GetAmmountOfCardPages();
                Console.WriteLine($"There are {ammountOfPages} cards to be fetched.");
                var confirm = _GetUserInput("Would you like to fetch the cards now (y/n)? ")
                                .ToString();

                if (confirm.Length > 1)
                {
                    _ShowError("Invalid input");
                    break;
                }

                Console.WriteLine(confirm);
            }
        }

        private static void _ShowError(string errorMessage)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.ResetColor();
        }

        static private object _GetUserInput(string message)
        {
            Console.Write(message);
            return Console.ReadLine();
        }

        static private int _GetAmmountOfCardPages()
        {
            var cardListWebDocument = new HtmlWeb().Load(baseUrl);
            var element = cardListWebDocument.GetElementbyId("cards-load-more");
            var numOfPages = int.Parse(element.Element("div").Element("span").InnerText.Substring(4));
            return 12 * numOfPages; // 12 is the number of cards on a page
        }

        static private int _GetCardsList()
        {
            return 0;
        }

        static private int _GetCardDetail()
        {
            return 0;
        }

    }
}
